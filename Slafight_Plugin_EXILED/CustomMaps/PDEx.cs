using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.Abilities;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.ProximityChat;
using UnityEngine;

using Slafight_Plugin_EXILED.API.Interface;

namespace Slafight_Plugin_EXILED.CustomMaps;

public class PDEx : IBootstrapHandler, System.IDisposable
{
    public static PDEx Instance { get; private set; }
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

    private bool _disposed;

    public PDEx()
    {
        Exiled.Events.Handlers.Server.RoundStarted += Setup;
        Exiled.Events.Handlers.Player.FailingEscapePocketDimension += JoinPDEx;
        Exiled.Events.Handlers.Player.Left += OnLeft;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Exiled.Events.Handlers.Server.RoundStarted -= Setup;
        Exiled.Events.Handlers.Player.FailingEscapePocketDimension -= JoinPDEx;
        Exiled.Events.Handlers.Player.Left -= OnLeft;
        Timing.KillCoroutines(handle);
        PDExPlayers.Clear();
        System.GC.SuppressFinalize(this);
    }

    public static List<Player> PDExPlayers = [];
    private CoroutineHandle handle;

    private void Setup()
    {
        PDExPlayers.Clear();
        Timing.KillCoroutines(handle);
        handle = Timing.RunCoroutine(Coroutine());
    }

    private static void OnLeft(LeftEventArgs ev)
    {
        if (ev.Player == null)
            return;

        PDExPlayers.RemoveAll(player => player?.ReferenceHub == null || player.Id == ev.Player.Id);
    }

    private static IEnumerator<float> Coroutine()
    {
        while (true)
        {
            if (!Round.InProgress) yield break;

            foreach (var player in Player.List.ToList())
            {
                if (player?.ReferenceHub == null) continue;
                if (player.Position.y >= -450f) continue;
                if (player.Zone == ZoneType.Pocket) continue;
                player.IsGodModeEnabled = true;
                player.EnableEffect<PocketCorroding>();
            }

            yield return Timing.WaitForSeconds(0.1f);

            foreach (var player in Player.List.ToList())
            {
                if (player?.ReferenceHub == null) continue;
                if (!player.IsEffectActive<PocketCorroding>()) continue;
                if (player.IsGodModeEnabled) player.IsGodModeEnabled = false;
            }

            yield return Timing.WaitForSeconds(0.9f); // 合計1秒
        }
    }

    private void JoinPDEx(FailingEscapePocketDimensionEventArgs ev)
    {
        if (Random.Range(0, 3) == 0)
        {
            int i = 0;
            foreach (var player in Player.List.ToList())
            {
                if (player?.ReferenceHub == null) continue;
                if (player.GetCustomRole() == CRoleTypeId.Scp106 || (player.GetCustomRole() == CRoleTypeId.None && player.Role.Type == RoleTypeId.Scp106))
                {
                    i++;
                }
            }
            if (i <= 0) return;
            ev.IsAllowed = false;
            ev.Player?.Position = MapFlags.PocketDimensionExitJoinPoint;
            ev.Player?.DisableEffect(EffectType.PocketCorroding);
            ev.Player?.EnableEffect(EffectType.Slowness, 30);
            if (ev.Player != null)
            {
                PDExPlayers.Add(ev.Player);
            }
            else
            {
                return;
            }
            foreach (var player in Player.List.ToList())
            {
                if (player?.ReferenceHub == null) continue;
                if (player.GetCustomRole() == CRoleTypeId.Scp106 || (player.GetCustomRole() == CRoleTypeId.None && player.Role.Type == RoleTypeId.Scp106))
                {
                    player.Position = MapFlags.PocketDimensionExitKingJoinPoint;
                    player.AddAbility(new AllowEscapeAbility(player));
                    if (player.IsConnected && !player.IsNPC)
                        player.ShowHint("アビリティ「腐蝕からの解放」が付与されました。\n人間を釈放したくなったら使ってください\nまた、近接チャットも一時的に利用可能です！");
                    Handler.CanUsePlayers.Add(player);
                }
            }
        }
    }
}
