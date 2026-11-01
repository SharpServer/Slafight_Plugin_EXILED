using System;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>再生が終わった理由です。</summary>
public enum SpeakerPlaybackEndReason
{
    /// <summary>クリップを最後まで送り切った。</summary>
    Completed,

    /// <summary><see cref="SpeakerApi.Stop"/> で止めた。</summary>
    Stopped,

    /// <summary>同じスピーカーで次のクリップが始まった。</summary>
    Replaced,
}

/// <summary>スピーカー再生のイベントに共通する情報です。</summary>
public abstract class SpeakerPlaybackEventArgs(SpeakerApi.Playback playback, bool loop, float duration) : EventArgs
{
    public SpeakerApi.Playback Playback { get; } = playback;

    public ExtendedSpeaker Speaker => Playback.Speaker;

    public string AudioPlayerName => Playback.AudioPlayerName;

    public string ClipName => Playback.ClipName;

    public byte ControllerId => Playback.ControllerId;

    /// <summary>この再生がループか。</summary>
    public bool Loop { get; } = loop;

    /// <summary>渡したクリップの尺(秒)。</summary>
    public float Duration { get; } = duration;
}

/// <summary>
/// <see cref="SpeakerApi.Play"/> などがサンプルを送る直前です。
/// <see cref="IsAllowed"/> を false にすると、この再生は始まりません。
/// </summary>
public sealed class SpeakerPlaybackStartingEventArgs(SpeakerApi.Playback playback, bool loop, float duration, bool destroyOnEnd)
    : SpeakerPlaybackEventArgs(playback, loop, duration)
{
    /// <summary>鳴り終わったあとスピーカーを破棄するか。</summary>
    public bool DestroyOnEnd { get; } = destroyOnEnd;

    public bool IsAllowed { get; set; } = true;
}

/// <summary>サンプルを送り、<see cref="ExtendedSpeaker.CurrentPlayback"/> を載せてからの直後です。</summary>
public sealed class SpeakerPlaybackStartedEventArgs(SpeakerApi.Playback playback, bool loop, float duration, bool destroyOnEnd)
    : SpeakerPlaybackEventArgs(playback, loop, duration)
{
    /// <summary>鳴り終わったあとスピーカーを破棄するか。</summary>
    public bool DestroyOnEnd { get; } = destroyOnEnd;
}

/// <summary>
/// 再生を片付ける直前です。
/// <see cref="IsAllowed"/> を false にすると、スピーカーは残し、再生も切りません。
/// </summary>
public sealed class SpeakerPlaybackStoppingEventArgs(SpeakerApi.Playback playback, bool loop, float duration, SpeakerPlaybackEndReason reason)
    : SpeakerPlaybackEventArgs(playback, loop, duration)
{
    public SpeakerPlaybackEndReason Reason { get; } = reason;

    public bool IsAllowed { get; set; } = true;
}

/// <summary>再生を片付けた直後です。差し替えのときはスピーカー自体は残っています。</summary>
public sealed class SpeakerPlaybackStoppedEventArgs(SpeakerApi.Playback playback, bool loop, float duration, SpeakerPlaybackEndReason reason)
    : SpeakerPlaybackEventArgs(playback, loop, duration)
{
    public SpeakerPlaybackEndReason Reason { get; } = reason;
}
