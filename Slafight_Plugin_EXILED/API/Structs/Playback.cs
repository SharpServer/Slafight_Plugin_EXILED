using System;
using Exiled.API.Features;
using MEC;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Structs;

public readonly struct Playback
{
    public Playback(string audioPlayerName, string clipName, ExtendedSpeaker speaker, byte controllerId, int generation = 0, CoroutineHandle cleanupHandle = default)
    {
        AudioPlayerName = audioPlayerName;
        ClipName = clipName;
        Speaker = speaker;
        ControllerId = controllerId;
        Generation = generation;
        CleanupHandle = cleanupHandle;
    }

    public string AudioPlayerName { get; }
    public string ClipName { get; }
    public ExtendedSpeaker Speaker { get; }
    public byte ControllerId { get; }

    /// <summary>このスピーカーで何回目の再生か。差し替えと、<see cref="SpeakerApi.PlaybackStopped"/> の対応に使う。</summary>
    public int Generation { get; }

    public CoroutineHandle CleanupHandle { get; }
    public bool IsValid => Speaker != null && !Speaker.IsDestroyed && ControllerId != 0;
    public float Volume => IsValid ? Speaker.Volume : 0f;

    /// <summary>
    /// この再生の残り秒数。別クリップに差し替わったあと、停止中、破棄後は 0。
    /// ループ再生では、いまの周回が終わるまでの残り。
    /// </summary>
    public float RemainingClipTime
    {
        get
        {
            if (!IsValid || Speaker.CurrentPlayback is not { } current || current.ControllerId != ControllerId || current.Generation != Generation)
                return 0f;

            if (!string.Equals(current.ClipName, ClipName, StringComparison.OrdinalIgnoreCase))
                return 0f;

            return Speaker.RemainingClipTime;
        }
    }

    public bool Stop()
        => SpeakerApi.Stop(this);

    public bool DestroyAudioPlayer()
        => Stop();

    public void SetTransform(Vector3 position, Transform? parent = null)
        => SpeakerApi.SetTransform(this, position, parent);

    public void SetVolume(float volume)
        => SpeakerApi.SetVolume(this, volume);

    public void SetListeners(Predicate<Player>? listeners)
        => SpeakerApi.SetListeners(this, listeners);
}
