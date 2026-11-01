using System;
using AdminToys;
using Mirror;
using UnityEngine;
using VoiceChat;
using LabSpeakerToy = LabApi.Features.Wrappers.SpeakerToy;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// LabAPI のスピーカートイを包む。EXILED の <c>Exiled.API.Features.Toys.Speaker</c> と同じく、
/// 音量・距離・ControllerId はこちら側から読む。
/// 残り時間は、渡したサンプルの尺と、LabAPI がパケットを進める <see cref="NetworkTime"/>、
/// 送信カーソルのいまの位置を合わせて出す。
/// </summary>
public sealed class ExtendedSpeaker
{
    private const float EndResetWindow = 0.02f;

    private SpeakerApi.Playback? _current;
    private double _anchorTime;
    private double _pauseStarted = -1d;
    private double _pausedAccumulated;
    private float _clipDuration;
    private bool _loop;
    private int _generation;
    private int _announcedGeneration;

    internal ExtendedSpeaker(LabSpeakerToy labSpeaker)
    {
        LabSpeaker = labSpeaker ?? throw new ArgumentNullException(nameof(labSpeaker));
    }

    /// <summary>ラップしている LabAPI スピーカー。</summary>
    public LabSpeakerToy LabSpeaker { get; }

    /// <summary>ゲーム側のスピーカートイ。</summary>
    public SpeakerToy Base => LabSpeaker.Base;

    /// <summary>このスピーカーでいま鳴っているクリップ。鳴り終わったあとと、ライブ送信のみのときは null。</summary>
    public SpeakerApi.Playback? CurrentPlayback
    {
        get
        {
            if (_current is not { } playback || playback.ControllerId != ControllerId || playback.Speaker != this)
                return null;

            if (!IsClipActive)
                return null;

            return playback;
        }
        internal set => _current = value;
    }

    public byte ControllerId
    {
        get => LabSpeaker.ControllerId;
        set => LabSpeaker.ControllerId = value;
    }

    public bool IsSpatial
    {
        get => LabSpeaker.IsSpatial;
        set => LabSpeaker.IsSpatial = value;
    }

    public float Volume
    {
        get => LabSpeaker.Volume;
        set => LabSpeaker.Volume = value;
    }

    public float MinDistance
    {
        get => LabSpeaker.MinDistance;
        set => LabSpeaker.MinDistance = value;
    }

    public float MaxDistance
    {
        get => LabSpeaker.MaxDistance;
        set => LabSpeaker.MaxDistance = value;
    }

    public bool IsDestroyed => LabSpeaker.IsDestroyed;

    public Transform Transform => LabSpeaker.Transform;

    public Vector3 Position
    {
        get => LabSpeaker.Position;
        set => LabSpeaker.Position = value;
    }

    public Func<LabApi.Features.Wrappers.Player, bool>? ValidPlayers
    {
        get => LabSpeaker.ValidPlayers;
        set => LabSpeaker.ValidPlayers = value;
    }

    /// <summary>ファイルから渡したクリップの尺(秒)。再生していなければ 0。</summary>
    public float ClipDuration => _clipDuration;

    internal bool Loop => _loop;

    internal int TakeGeneration() => ++_generation;

    internal bool IsAnnounced(SpeakerApi.Playback playback)
        => _announcedGeneration != 0 && playback.Generation == _announcedGeneration;

    internal bool TryGetAnnounced(out SpeakerApi.Playback playback)
    {
        if (_current is { } current && IsAnnounced(current))
        {
            playback = current;
            return true;
        }

        playback = default;
        return false;
    }

    internal void MarkAnnounced(SpeakerApi.Playback playback)
    {
        _current = playback;
        _announcedGeneration = playback.Generation;
    }

    internal void MarkEnded()
    {
        _announcedGeneration = 0;
        _current = null;
    }

    /// <summary>送信がまだ続いているか、一時停止中か。</summary>
    internal bool IsClipActive
    {
        get
        {
            if (IsDestroyed || _clipDuration <= 0f)
                return false;

            return LabSpeaker.IsPlaying || LabSpeaker.IsPaused;
        }
    }

    /// <summary>
    /// いま鳴っているクリップの残り秒数。停止中と破棄後は 0。
    /// ループ再生では、いまの周回が終わるまでの残り。
    /// </summary>
    public float RemainingClipTime
    {
        get
        {
            if (IsDestroyed || _clipDuration <= 0f || (!LabSpeaker.IsPlaying && !LabSpeaker.IsPaused))
                return 0f;

            float fromSync = RemainingFromSync();
            int sampleCount = LabSpeaker.CurrentSampleCount;
            if (sampleCount <= 0)
                return fromSync;

            float cursorDuration = sampleCount * VoiceChatSettings.SampleToDuartionRate;
            float fromCursor = Mathf.Max(0f, (sampleCount - LabSpeaker.CurrentPosition) * VoiceChatSettings.SampleToDuartionRate);

            // 終端で LabAPI は再生位置を 0 に戻す。尺の終わり付近でカーソルだけが先頭に見えるときは、同期時刻を優先する。
            if (!_loop && fromSync <= EndResetWindow && LabSpeaker.CurrentPosition < VoiceChatSettings.PacketSizePerChannel && fromCursor > fromSync)
                return fromSync;

            // カーソルが見ているバッファが、渡したファイルの尺と一致するときだけカーソルを使う。
            if (Mathf.Abs(cursorDuration - _clipDuration) <= EndResetWindow)
                return fromCursor;

            return fromSync;
        }
    }

    public void Play(float[] samples, bool queue = true, bool loop = false)
    {
        float duration = samples == null || samples.Length == 0
            ? 0f
            : samples.Length * VoiceChatSettings.SampleToDuartionRate;

        bool continueQueue = queue && _clipDuration > 0f && (LabSpeaker.IsPlaying || LabSpeaker.IsPaused);
        if (!continueQueue)
        {
            _clipDuration = duration;
            _anchorTime = NetworkTime.time;
            _pauseStarted = -1d;
            _pausedAccumulated = 0d;
        }
        else
        {
            _clipDuration += duration;
        }

        _loop = loop;
        LabSpeaker.Play(samples, queue, loop);
    }

    public void Stop()
    {
        ClearClock();
        LabSpeaker.Stop();
    }

    public void Spawn()
        => LabSpeaker.Spawn();

    public void Destroy()
    {
        ClearClock();
        LabSpeaker.Destroy();
    }

    internal void ClearCurrentPlayback(byte controllerId)
    {
        if (_current is { } current && current.ControllerId == controllerId)
            _current = null;
    }

    private float RemainingFromSync()
    {
        double elapsed = SyncedElapsed();
        if (_loop && _clipDuration > 0f)
        {
            double into = elapsed % _clipDuration;
            return (float)(_clipDuration - into);
        }

        return (float)Math.Max(0d, _clipDuration - elapsed);
    }

    private double SyncedElapsed()
    {
        ObservePause();
        double now = _pauseStarted >= 0d ? _pauseStarted : NetworkTime.time;
        double elapsed = now - _anchorTime - _pausedAccumulated;
        return elapsed < 0d ? 0d : elapsed;
    }

    private void ObservePause()
    {
        if (IsDestroyed)
            return;

        if (LabSpeaker.IsPaused)
        {
            if (_pauseStarted < 0d)
                _pauseStarted = NetworkTime.time;
            return;
        }

        if (_pauseStarted < 0d)
            return;

        _pausedAccumulated += NetworkTime.time - _pauseStarted;
        _pauseStarted = -1d;
    }

    private void ClearClock()
    {
        _current = null;
        _announcedGeneration = 0;
        _clipDuration = 0f;
        _loop = false;
        _pauseStarted = -1d;
        _pausedAccumulated = 0d;
    }
}
