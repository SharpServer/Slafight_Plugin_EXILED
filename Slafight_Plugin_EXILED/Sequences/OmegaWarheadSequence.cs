using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Structs;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Sequences;

public class OmegaWarheadSequence : SequenceBase
{
    public override string Name => "OMEGA WARHEAD";
    public override string Description => "全施設を爆破するぜYEAH";
    public override byte Priority { get; set; } = 255;
    
    public Playback Playback { get; private set; }

    protected override void RegisterEvents()
    {
        SpeakerApi.PlaybackStopped += Boom;
        base.RegisterEvents();
    }

    protected override void UnregisterEvents()
    {
        SpeakerApi.PlaybackStopped -= Boom;
        base.UnregisterEvents();
    }

    protected override void OnTrigger(SequenceRun scope)
    {
        Warhead.Stop();

        foreach (var room in Room.List)
        {
            if (room == null) continue;
            room.Color = Color.blue;
        }
        foreach (Door door in Door.List)
        {
            if (door.Type != DoorType.ElevatorGateA &&
                door.Type != DoorType.ElevatorGateB &&
                door.Type != DoorType.ElevatorLczA &&
                door.Type != DoorType.ElevatorLczB &&
                door.Type != DoorType.ElevatorNuke &&
                door.Type != DoorType.ElevatorScp049 &&
                door.Type != DoorType.ElevatorServerRoom)
            {
                door.IsOpen = true;
                door.PlaySound(DoorBeepType.InteractionAllowed);
                door.Lock(DoorLockType.Warhead);
            }
        }

        Playback = SpeakerApi.Play("./omega_v2.ogg", "OmegaWarhead", Vector3.zero, destroyOnEnd: true, maxDistance: 99999f);
        if (!Playback.IsValid)
            throw new System.InvalidOperationException("OMEGA WARHEAD could not start ./omega_v2.ogg.");

        var seconds = Mathf.CeilToInt(Playback.Speaker.ClipDuration);
        Log.Debug($"[Slafight] OMEGA WARHEAD audio started (controller {Playback.ControllerId}, countdown {seconds}s).");
        Exiled.API.Features.Cassie.MessageTranslated($"$PITCH_.95 EMERGENCY OMEGA WARHEAD DETONATION SEQUENCE, ACTIVATED. THE ALL FACILITY SECTOR IS _SUFFIX_EST TO SELF DESTRUCT IN T MINUS {seconds} SECONDS.",
            $"<color=blue><b>緊急OMEGA WARHEAD起爆システム起動。</b></color><split>全施設は{seconds}秒後に爆破されます。");
    }

    private void Boom(SpeakerPlaybackStoppedEventArgs ev)
    {
        if (ev.Playback.AudioPlayerName != Playback.AudioPlayerName) return;
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
