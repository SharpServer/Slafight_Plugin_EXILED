using System.Collections.Generic;
using System;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Structs;

public readonly struct LivePlayback
{
    public LivePlayback(string audioPlayerName, ExtendedSpeaker speaker, byte controllerId)
    {
        AudioPlayerName = audioPlayerName;
        Speaker = speaker;
        ControllerId = controllerId;
    }

    public string AudioPlayerName { get; }
    public ExtendedSpeaker Speaker { get; }
    public byte ControllerId { get; }
    public bool IsValid => Speaker != null && !Speaker.IsDestroyed && ControllerId != 0;
    public float Volume => IsValid ? Speaker.Volume : 0f;

    public bool DestroyAudioPlayer()
        => SpeakerApi.DestroyLiveSpeaker(this);

    public void SetTransform(Vector3 position, Transform? parent = null)
        => SpeakerApi.SetTransform(this, position, parent);

    public void SetVolume(float volume)
        => SpeakerApi.SetVolume(this, volume);

    public void SetListeners(Predicate<Player>? listeners)
        => SpeakerApi.SetListeners(this, listeners);

    public int SendFrame(byte[] data, int dataLength, IEnumerable<ReferenceHub> targets)
        => SpeakerApi.SendAudioFrame(this, data, dataLength, targets);
}
