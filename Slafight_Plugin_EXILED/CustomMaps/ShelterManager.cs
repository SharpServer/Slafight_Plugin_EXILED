using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Interface;

namespace Slafight_Plugin_EXILED.CustomMaps;

public class ShelterManager : IBootstrapHandler
{
    public static void Register()
    {
        Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;
    }

    public static void Unregister()
    {
        Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;
    }

    public static bool FirstFlag { get; set; } = false;
    public static bool LightIsOn { get; set; } = true;

    private static void OnRoundStarted()
    {
        FirstFlag = false;
        LightIsOn = true;
        SpeakerApi.LoadClip("Blackout.ogg");
        SpeakerApi.LoadClip("Elec_Idle.ogg");
        SpeakerApi.LoadClip("PowerUp.ogg");
    }
}