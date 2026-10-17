using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Warhead;
using MapGeneration.Holidays;
using MEC;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Server = Exiled.Events.Handlers.Server;
using Warhead = Exiled.Events.Handlers.Warhead;

namespace Slafight_Plugin_EXILED.Changes;

public static class FacilityLightHandler
{
    private const byte NightVisionIntensity = 255;
    private const float NightVisionDuration = 2.5f;
    private const float NightVisionRefreshInterval = 1.5f;

    /// <summary>Warhead 系の照明遷移は素早く切り替える。</summary>
    public const float WarheadFadeDuration = 0.5f;

    // Raid/Attack 系イベントが設定した部屋色。Warhead 停止などで通常色へ戻す際、これを優先する。
    private static readonly Dictionary<Room, Color> EventRoomColors = [];

    private static readonly HashSet<int> ForceNightVisionPlayers = [];
    private static readonly HashSet<CTeam> ForceNightVisionTeams = [];
    private static CoroutineHandle _forceNightVisionCoroutineHandle;

    public static void Register()
    {
        Server.WaitingForPlayers += OnWaitingForPlayers;
        Server.RoundStarted += OnRoundStarted;
        Exiled.Events.Handlers.Player.Left += OnLeft;
        Warhead.Starting += OnWarhead;
        Warhead.Stopping += OnStopping;
    }

    public static void Unregister()
    {
        Server.WaitingForPlayers -= OnWaitingForPlayers;
        Server.RoundStarted -= OnRoundStarted;
        Exiled.Events.Handlers.Player.Left -= OnLeft;
        Warhead.Starting -= OnWarhead;
        Warhead.Stopping -= OnStopping;
        Timing.KillCoroutines(_forceNightVisionCoroutineHandle);
        EventRoomColors.Clear();
        AlarmLight.CancelReplacement();
        ClearForcedNightVision();
    }

    private static void OnWaitingForPlayers()
    {
        EventRoomColors.Clear();
        Timing.KillCoroutines(_forceNightVisionCoroutineHandle);
        AlarmLight.CancelReplacement();
        ClearForcedNightVision();
    }
    
    private static void OnRoundStarted()
    {
        _forceNightVisionCoroutineHandle = Timing.RunCoroutine(ForceNightVisionCoroutine());
        Timing.CallDelayed(0.5f, () => InitLight());
    }

    private static void InitLight(float duration = RoomColorFader.DefaultDuration)
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

            if (EventRoomColors.TryGetValue(room, out var eventColor))
            {
                room.FadeColor(eventColor, duration);
                continue;
            }

            switch (room.Zone)
            {
                case ZoneType.Surface:
                    room.FadeColor(surface, duration);
                    break;
                case ZoneType.Entrance:
                    room.FadeColor(room.Type switch
                    {
                        RoomType.EzIntercom => intercom,
                        RoomType.EzVent or RoomType.EzShelter => alert,
                        _ => facility
                    }, duration);
                    break;
                case ZoneType.HeavyContainment:
                    room.FadeColor(facility, duration);
                    break;
                case ZoneType.LightContainment:
                    room.FadeColor(room.Type == RoomType.LczAirlock ? alert : lightContainment, duration);
                    break;
            }
        }
    }

    /// <summary>イベント照明を解除して通常色へ戻す。</summary>
    public static void TurnToNormal()
    {
        EventRoomColors.Clear();
        InitLight();
    }

    /// <summary>イベント用の部屋色を設定する。Warhead 停止時もこの色へ戻る。</summary>
    public static void SetEventLight(this Room room, Color color, float duration = RoomColorFader.DefaultDuration)
    {
        if (room == null)
            return;

        EventRoomColors[room] = color;
        room.FadeColor(color, duration);
    }

    public static void OnWarhead(StartingEventArgs ev)
    {
        if (!ev.IsAllowed) return;
        ColorUtility.TryParseHtmlString("#ff1500", out var color);
        foreach (var room in Room.List)
            room.FadeColor(color, WarheadFadeDuration);
        AlarmLight.SetAlarmForAll(true);
    }

    public static void OnStopping(StoppingEventArgs ev)
    {
        if (!ev.IsAllowed) return;
        InitLight(WarheadFadeDuration);
        AlarmLight.SetAlarmForAll(false);
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
