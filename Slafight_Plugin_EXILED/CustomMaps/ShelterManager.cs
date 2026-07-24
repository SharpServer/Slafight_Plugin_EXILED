using Exiled.Events.Handlers;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Interface;

namespace Slafight_Plugin_EXILED.CustomMaps;

public class ShelterManager : IBootstrapHandler
{
    public static void Register()
    {
        Server.RoundStarted += OnRoundStarted;
    }

    public static void Unregister()
    {
        Server.RoundStarted -= OnRoundStarted;
    }

    public static bool FirstFlag { get; set; }
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