using Exiled.API.Enums;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Enums;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps;

public class OmegaWarhead : EventHandlerBase
{
    public static bool IsStarted { get; private set; }
    public static bool IsAvailable => CanBeStart();
    public static SpeakerApi.Playback Playback { get; private set; }
    
    public override void RegisterEvents()
    {
        SpeakerApi.PlaybackStopped += Boom;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        SpeakerApi.PlaybackStopped -= Boom;
        base.UnregisterEvents();
    }

    private static bool CanBeStart()
    {
        if (IsStarted) return false;
        return true;
    }

    public static bool TryStart(bool isOverrideAlpha)
    {
        if (!CanBeStart()) return false;
        if (isOverrideAlpha)
        {
            Warhead.Stop();
        }

        foreach (var room in Room.List)
        {
            if (room == null) continue;
            room.Color = Color.blue;
        }

        Playback = SpeakerApi.Play("./omega_v2.ogg", "OmegaWarhead", Vector3.zero, maxDistance: 99999f);
        
        return true;
    }

    private void Boom(SpeakerPlaybackStoppedEventArgs ev)
    {
        if (ev.Playback.ControllerId != Playback.ControllerId) return;
        if (ev.Reason != SpeakerPlaybackEndReason.Completed) return;
        foreach (var player in Player.List)
        {
            if (!player.IsSafePlayer()) continue;
            player.SendWarheadExplosionEffect();
            player.ExplodeEffect(ProjectileType.FragGrenade);
            player.Kill("OMEGA WARHEADに爆破された");
        }
    }
}