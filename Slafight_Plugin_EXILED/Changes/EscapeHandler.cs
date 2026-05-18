using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Player = Exiled.API.Features.Player;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.SpecialEvents;

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
    public PlayerCustomEscapedEventArgs(Player player) => Player = player;
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
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Exiled.Events.Handlers.Player.Escaping -= CancelDefaultEscape;
        Exiled.Events.Handlers.Server.RoundStarted -= AddEscapeCoroutine;
        if (_escapeCoroutine.IsRunning)
            Timing.KillCoroutines(_escapeCoroutine);
        if (_setupCoroutine.IsRunning)
            Timing.KillCoroutines(_setupCoroutine);
        MapFlags.EscapePoints.Clear();
        ClearEscapeOverrides();
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
        => DynamicOverrides.Add(rule);

    public static void ClearEscapeOverrides()
        => DynamicOverrides.Clear();

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

    private EscapeTargetRole GetEscapeTarget(CTeam myTeam, CTeam cufferTeam)
    {
        return (myTeam, cufferTeam) switch
        {
            // Class-D Personnel
            (CTeam.ClassD, CTeam.FoundationForces or CTeam.Scientists or CTeam.Guards)
                => new EscapeTargetRole { Vanilla = RoleTypeId.NtfPrivate, Custom = null },
            (CTeam.ClassD, CTeam.Fifthists)
                => new EscapeTargetRole { Vanilla = null, Custom = CRoleTypeId.FifthistConvert },
            (CTeam.ClassD, CTeam.GoC)
                => new EscapeTargetRole { Vanilla = null, Custom = CRoleTypeId.GoCOperative },
            (CTeam.ClassD, _)
                => new EscapeTargetRole { Vanilla = RoleTypeId.ChaosConscript, Custom = null },

            // Scientists
            (CTeam.Scientists, CTeam.ChaosInsurgency or CTeam.ClassD)
                => new EscapeTargetRole { Vanilla = RoleTypeId.ChaosConscript, Custom = null },
            (CTeam.Scientists, CTeam.Fifthists)
                => new EscapeTargetRole { Vanilla = null, Custom = CRoleTypeId.FifthistConvert },
            (CTeam.Scientists, CTeam.GoC)
                => new EscapeTargetRole { Vanilla = null, Custom = CRoleTypeId.GoCOperative },
            (CTeam.Scientists, _)
                => new EscapeTargetRole { Vanilla = RoleTypeId.NtfSpecialist, Custom = null },

            // Chaos Insurgency
            (CTeam.ChaosInsurgency, CTeam.FoundationForces or CTeam.Scientists or CTeam.Guards)
                => new EscapeTargetRole { Vanilla = RoleTypeId.NtfPrivate, Custom = null },
            (CTeam.ChaosInsurgency, CTeam.Fifthists)
                => new EscapeTargetRole { Vanilla = null, Custom = CRoleTypeId.FifthistConvert },
            (CTeam.ChaosInsurgency, CTeam.GoC)
                => new EscapeTargetRole { Vanilla = null, Custom = CRoleTypeId.GoCOperative },

            // Foundation Forces (with Guards)
            (CTeam.FoundationForces or CTeam.Guards, CTeam.ChaosInsurgency or CTeam.ClassD)
                => new EscapeTargetRole { Vanilla = RoleTypeId.ChaosConscript, Custom = null },
            (CTeam.FoundationForces or CTeam.Guards, CTeam.Fifthists)
                => new EscapeTargetRole { Vanilla = null, Custom = CRoleTypeId.FifthistConvert },
            (CTeam.FoundationForces or CTeam.Guards, CTeam.GoC)
                => new EscapeTargetRole { Vanilla = null, Custom = CRoleTypeId.GoCOperative },

            // Fifthists
            (CTeam.Fifthists, CTeam.ChaosInsurgency or CTeam.ClassD)
                => new EscapeTargetRole { Vanilla = RoleTypeId.ChaosConscript, Custom = null },
            (CTeam.Fifthists, CTeam.FoundationForces or CTeam.Scientists or CTeam.Guards)
                => new EscapeTargetRole { Vanilla = RoleTypeId.NtfPrivate, Custom = null },

            // GoC
            (CTeam.Fifthists, CTeam.GoC)
                => new EscapeTargetRole { Vanilla = null, Custom = CRoleTypeId.GoCOperative },

            // デフォルト
            _ => new EscapeTargetRole { Vanilla = null, Custom = null },
        };
    }

    private EscapeTargetRole ApplyEventOverrides(EscapeTargetRole baseTarget, Player player)
    {
        var nowEvent = SpecialEventsHandler.Instance.NowEvent;

        switch (nowEvent)
        {
            case SpecialEventType.None:
            default:
                return baseTarget;

            // 他イベントもここへ
        }
    }

    private EscapeTargetRole GetEscapeTarget(Player player)
    {
        var roleOverride = CheckRoleEscapeOverride(player);
        if (roleOverride.Vanilla.HasValue || roleOverride.Custom.HasValue)
            return roleOverride;

        var myTeam = player.GetTeam();
        var cufferTeam = player.Cuffer?.GetTeam() ?? CTeam.Null;
        var baseTarget = GetEscapeTarget(myTeam, cufferTeam);

        return ApplyEventOverrides(baseTarget, player);
    }

    private EscapeTargetRole CheckRoleEscapeOverride(Player player)
    {
        // 動的オーバーライドを最優先
        foreach (var rule in DynamicOverrides)
        {
            var result = rule(player);
            if (result.HasValue && (result.Value.Vanilla.HasValue || result.Value.Custom.HasValue))
                return result.Value;
        }

        return player.GetCustomRole() switch
        {
            CRoleTypeId.Scp3005 => new EscapeTargetRole { Vanilla = null, Custom = CRoleTypeId.FifthistPriest },
            _ => new EscapeTargetRole { Vanilla = null, Custom = null }
        };
    }

    public void Escape(Player player)
    {
        Log.Debug($"Escape: {player.Nickname} ({player.GetTeam()})");

        var target = GetEscapeTarget(player);
        if (target.Vanilla is null && target.Custom is null) return;

        var ev = new PlayerCustomEscapingEventArgs(player) { IsAllowed = true };
        PlayerCustomEscaping?.Invoke(null, ev);
        if (!ev.IsAllowed) return;

        SaveItems(player);

        if (target.Custom is { } custom) player.SetRole(custom);
        else if (target.Vanilla is { } vanilla) player.SetRole(vanilla);

        PlayerCustomEscaped?.Invoke(null, new PlayerCustomEscapedEventArgs(player));
    }

    private CoroutineHandle _escapeCoroutine;
    private CoroutineHandle _setupCoroutine;

    public void AddEscapeCoroutine()
    {
        if (_escapeCoroutine.IsRunning)
            Timing.KillCoroutines(_escapeCoroutine);

        if (_setupCoroutine.IsRunning)
            Timing.KillCoroutines(_setupCoroutine);

        _setupCoroutine = Timing.CallDelayed(2.25f, () => 
        {
            _escapeCoroutine = Timing.RunCoroutine(EscapeCoroutine());
        });
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
