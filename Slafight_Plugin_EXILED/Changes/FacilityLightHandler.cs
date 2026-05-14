using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Warhead;
using MEC;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Changes;

public static class FacilityLightHandler
{
    public struct RoomColorData
    {
        public string ColorCode;
    }
    
    public static void Register()
    {
        Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;
        Exiled.Events.Handlers.Warhead.Starting += OnWarhead;
        Exiled.Events.Handlers.Warhead.Stopping += OnStopping;
    }

    public static void Unregister()
    {
        Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;
        Exiled.Events.Handlers.Warhead.Starting -= OnWarhead;
        Exiled.Events.Handlers.Warhead.Stopping -= OnStopping;
    }

    private static void OnRoundStarted()
    {
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
}
