using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Warhead;
using MEC;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Changes;

public static class FacilityLightHandler
{
    private const byte NightVisionIntensity = 255;
    private const float NightVisionDuration = 2.5f;
    private const float NightVisionRefreshInterval = 1.5f;

    private static readonly HashSet<int> ForceNightVisionPlayers = [];
    private static readonly HashSet<CTeam> ForceNightVisionTeams = [];
    private static CoroutineHandle ForceNightVisionCoroutineHandle;

    public static void Register()
    {
        Exiled.Events.Handlers.Server.WaitingForPlayers += OnWaitingForPlayers;
        Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;
        Exiled.Events.Handlers.Player.Left += OnLeft;
        Exiled.Events.Handlers.Warhead.Starting += OnWarhead;
        Exiled.Events.Handlers.Warhead.Stopping += OnStopping;
    }

    public static void Unregister()
    {
        Exiled.Events.Handlers.Server.WaitingForPlayers -= OnWaitingForPlayers;
        Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;
        Exiled.Events.Handlers.Player.Left -= OnLeft;
        Exiled.Events.Handlers.Warhead.Starting -= OnWarhead;
        Exiled.Events.Handlers.Warhead.Stopping -= OnStopping;
        Timing.KillCoroutines(ForceNightVisionCoroutineHandle);
        ClearForcedNightVision();
    }

    private static void OnWaitingForPlayers()
    {
        Timing.KillCoroutines(ForceNightVisionCoroutineHandle);
        ClearForcedNightVision();
    }
    
    private static void OnRoundStarted()
    {
        ForceNightVisionCoroutineHandle = Timing.RunCoroutine(ForceNightVisionCoroutine());
        Timing.CallDelayed(0.5f, InitLight);
    }

    private static void InitLight()
    {
        ColorUtility.TryParseHtmlString("#c1eaff", out var surface);
        ColorUtility.TryParseHtmlString("#9bddff", out var facility);
        ColorUtility.TryParseHtmlString("#fcd4b0", out var lightContainment);
        ColorUtility.TryParseHtmlString("#FFBCBC", out var intercom);
        ColorUtility.TryParseHtmlString("#FF0000", out var alert);

        foreach (var room in Room.List)
        {
            if (room == null)
                continue;

            switch (room.Zone)
            {
                case ZoneType.Surface:
                    room.Color = surface;
                    break;
                case ZoneType.Entrance:
                    room.Color = room.Type switch
                    {
                        RoomType.EzIntercom => intercom,
                        RoomType.EzVent or RoomType.EzShelter => alert,
                        _ => facility
                    };
                    break;
                case ZoneType.HeavyContainment:
                    room.Color = facility;
                    break;
                case ZoneType.LightContainment:
                    room.Color = room.Type == RoomType.LczAirlock ? alert : lightContainment;
                    break;
            }
        }
    }

    public static void TurnToNormal() => InitLight();

    public static void OnWarhead(StartingEventArgs ev)
    {
        if (!ev.IsAllowed) return;
        ColorUtility.TryParseHtmlString("#ff1500", out var color);
        foreach (var room in Room.List)
            room.Color = color;
    }

    public static void OnStopping(StoppingEventArgs ev)
    {
        if (!ev.IsAllowed) return;
        InitLight();
    }

    private static void OnLeft(LeftEventArgs ev)
    {
        if (ev.Player != null)
            ForceNightVisionPlayers.Remove(ev.Player.Id);
    }

    public static void SetNightVisionTargetState(this Player targetPlayer, bool isEnabled)
    {
        if (targetPlayer == null)
            return;

        if (isEnabled)
            ForceNightVisionPlayers.Add(targetPlayer.Id);
        else
            ForceNightVisionPlayers.Remove(targetPlayer.Id);
    }

    public static void SetNightVisionTargetStateForTeam(this CTeam targetTeam, bool isEnabled)
    {
        if (isEnabled)
            ForceNightVisionTeams.Add(targetTeam);
        else
            ForceNightVisionTeams.Remove(targetTeam);
    }

    public static bool IsNightVisionForced(this Player player)
    {
        return player != null &&
               (ForceNightVisionPlayers.Contains(player.Id) || ForceNightVisionTeams.Contains(player.GetTeam()));
    }

    public static void ClearForcedNightVision()
    {
        ForceNightVisionPlayers.Clear();
        ForceNightVisionTeams.Clear();
    }
    
    private static IEnumerator<float> ForceNightVisionCoroutine()
    {
        while (true)
        {
            if (Round.IsLobby) yield break;

            foreach (var player in Player.List)
            {
                if (!player.IsAlive || !player.IsNightVisionForced())
                    continue;

                player.EnableEffect<NightVision>(NightVisionIntensity, NightVisionDuration);
            }

            yield return Timing.WaitForSeconds(NightVisionRefreshInterval);
        }
    }
}
