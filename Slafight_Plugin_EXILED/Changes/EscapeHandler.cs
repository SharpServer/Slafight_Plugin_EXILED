using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.RoundVictory.Core;
using Slafight_Plugin_EXILED.API.Features.Teams.Escape;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Player = Exiled.API.Features.Player;
using Slafight_Plugin_EXILED.API.Interface;

namespace Slafight_Plugin_EXILED.Changes;

public class PlayerCustomEscapingEventArgs : EventArgs
{
    public Player Player { get; }
    public bool IsAllowed { get; set; } = true;
    public PlayerCustomEscapingEventArgs(Player player) => Player = player;
}

public class PlayerCustomEscapedEventArgs : EventArgs
{
    public Player Player { get; }
    public CTeam EscapedTeam { get; }
    public PlayerCustomEscapedEventArgs(Player player, CTeam escapedTeam)
    {
        Player = player;
        EscapedTeam = escapedTeam;
    }
}

public class EscapeHandler : IBootstrapHandler, IDisposable
{
    public static EscapeHandler Instance { get; private set; }
    public static void Register()
    {
        Unregister();
        Instance = new();
    }

    public static void Unregister()
    {
        Instance?.Dispose();
        Instance = null;
    }

    public static event EventHandler<PlayerCustomEscapingEventArgs> PlayerCustomEscaping;
    public static event EventHandler<PlayerCustomEscapedEventArgs> PlayerCustomEscaped;

    private bool _disposed;

    public EscapeHandler()
    {

        Exiled.Events.Handlers.Player.Escaping += CancelDefaultEscape;
        Exiled.Events.Handlers.Server.RoundStarted += AddEscapeCoroutine;
        Exiled.Events.Handlers.Server.RestartingRound += ResetRoundState;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Exiled.Events.Handlers.Player.Escaping -= CancelDefaultEscape;
        Exiled.Events.Handlers.Server.RoundStarted -= AddEscapeCoroutine;
        Exiled.Events.Handlers.Server.RestartingRound -= ResetRoundState;
        ResetRoundState();
        MapFlags.EscapePoints.Clear();
        PlayerCustomEscaping = null;
        PlayerCustomEscaped = null;
        GC.SuppressFinalize(this);
    }

    private const float EscapeRadius = 1.75f;
    private const float EscapeRadiusSqr = EscapeRadius * EscapeRadius;
    private const float ItemPickupRadius = 1.05f;
    private const float ItemPickupRadiusSqr = ItemPickupRadius * ItemPickupRadius;

    public IReadOnlyList<Vector3> EscapePoints => MapFlags.EscapePoints;

    // =====================
    //  動的オーバーライド
    // =====================

    public static readonly List<Func<Player, EscapeTargetRole?>> DynamicOverrides = [];

    public static void AddEscapeOverride(Func<Player, EscapeTargetRole?> rule)
    {
        DynamicOverrides.Add(rule);
        CTeamEscapeRegistry.AddDynamicOverride(context =>
        {
            var result = rule(context.Player);
            return result.HasValue
                ? new CTeamEscapeTarget(result.Value.Vanilla, result.Value.Custom)
                : null;
        });
    }

    public static void ClearEscapeOverrides()
    {
        DynamicOverrides.Clear();
        CTeamEscapeRegistry.ClearDynamicOverrides();
    }

    public static void AddRoleEscapeOverride(RoleTypeId role, CRoleTypeId? custom = null, RoleTypeId? vanilla = null)
        => AddEscapeOverride(p => p.Role.Type == role
            ? new EscapeTargetRole { Custom = custom, Vanilla = vanilla }
            : null);

    public static void AddCustomRoleEscapeOverride(CRoleTypeId role, CRoleTypeId? custom = null, RoleTypeId? vanilla = null)
        => AddEscapeOverride(p => p.GetCustomRole() == role
            ? new EscapeTargetRole { Custom = custom, Vanilla = vanilla }
            : null);

    // =====================

    public void SaveItems(Player player)
    {
        var nowPos = player.Position;
        player.DropItems();

        var saveItems = Pickup.List
            .Where(p => p != null && p.PreviousOwner == player && (p.Position - nowPos).sqrMagnitude <= ItemPickupRadiusSqr)
            .ToList();

        if (saveItems.Count == 0) return;

        Timing.CallDelayed(0.5f, () =>
        {
            if (player?.IsConnected != true) return;
            var newPos = player.Position + new Vector3(0f, 0.15f, 0f);
            foreach (var item in saveItems)
                if (item?.IsSpawned == true) item.Position = newPos;
        });
    }

    public struct EscapeTargetRole
    {
        public RoleTypeId? Vanilla;
        public CRoleTypeId? Custom;
    }

    public void Escape(Player player)
    {
        var escapedTeam = player.GetTeam();
        Log.Debug($"Escape: {player.Nickname} ({escapedTeam})");

        var target = CTeamEscapeRegistry.Resolve(player);
        if (target.IsEmpty) return;

        var ev = new PlayerCustomEscapingEventArgs(player) { IsAllowed = true };
        PlayerCustomEscaping?.Invoke(null, ev);
        if (!ev.IsAllowed) return;

        SaveItems(player);

        if (target.Custom is { } custom) player.SetRole(custom);
        else if (target.Vanilla is { } vanilla) player.SetRole(vanilla);

        EvacuationRoundEndState.RecordEscape(escapedTeam);
        PlayerCustomEscaped?.Invoke(null, new PlayerCustomEscapedEventArgs(player, escapedTeam));
    }

    private CoroutineHandle _escapeCoroutine;
    private CoroutineHandle _setupCoroutine;

    public void AddEscapeCoroutine()
    {
        ResetRoundState();

        _setupCoroutine = Timing.CallDelayed(2.25f, () => 
        {
            _escapeCoroutine = Timing.RunCoroutine(EscapeCoroutine());
        });
    }

    private void ResetRoundState()
    {
        if (_escapeCoroutine.IsRunning)
            Timing.KillCoroutines(_escapeCoroutine);

        if (_setupCoroutine.IsRunning)
            Timing.KillCoroutines(_setupCoroutine);

        ClearEscapeOverrides();
        EvacuationRoundEndState.Reset();
    }

    private IEnumerator<float> EscapeCoroutine()
    {
        var escapedPlayers = new HashSet<int>();
        var escapeTimers = new Dictionary<int, CoroutineHandle>();

        for (;;)
        {
            if (Round.IsLobby) yield break;
            if (EscapePoints.Count == 0) { yield return Timing.WaitForSeconds(0.5f); continue; }

            foreach (var player in Player.List)
            {
                if (player?.IsAlive != true) continue;

                if (escapeTimers.TryGetValue(player.Id, out var timer) && !timer.IsRunning)
                {
                    escapedPlayers.Remove(player.Id);
                    escapeTimers.Remove(player.Id);
                }

                if (escapedPlayers.Contains(player.Id)) continue;

                var playerPos = player.Position;
                for (int i = 0; i < EscapePoints.Count; i++)
                {
                    if ((playerPos - EscapePoints[i]).sqrMagnitude <= EscapeRadiusSqr)
                    {
                        Escape(player);
                        escapedPlayers.Add(player.Id);

                        escapeTimers[player.Id] = Timing.CallDelayed(5f, () =>
                        {
                            escapedPlayers.Remove(player.Id);
                            escapeTimers.Remove(player.Id);
                        });
                        break;
                    }
                }
            }
            yield return Timing.WaitForSeconds(0.5f);
        }
    }

    public void CancelDefaultEscape(EscapingEventArgs ev) => ev.IsAllowed = false;
}
