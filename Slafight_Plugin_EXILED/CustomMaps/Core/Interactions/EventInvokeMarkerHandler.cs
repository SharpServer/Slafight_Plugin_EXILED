using CustomPlayerEffects;
using CustomRendering;
using ProjectMER.Events.Arguments;
using ProjectMER.Events.Handlers;
using Slafight_Plugin_EXILED.API.Interface;

namespace Slafight_Plugin_EXILED.CustomMaps.Core.Interactions;

public class EventInvokeMarkerHandler : IBootstrapHandler
{
    public static void Register()
    {
        EventInvokeMarker.Invoked += OnInvoked;
    }

    public static void Unregister()
    {
        EventInvokeMarker.Invoked -= OnInvoked;
    }

    private static void OnInvoked(EventInvokeMarkerInvokedEventArgs ev)
    {
        if (ev.Player is null) return;
        switch (ev.Tag)
        {
            case "EzShelterFog":
                if (ev.Player.TryGetEffect(out FogControl _))
                {
                    ev.Player.DisableEffect<FogControl>();
                }
                else
                {
                    ev.Player.EnableEffect<FogControl>(255);
                    ev.Player.GetEffect<FogControl>()?.SetFogType(FogType.Inside);
                }
                break;
        }
    }
}