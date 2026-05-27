using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using UnityEngine;
using VoiceChat;
using VoiceChat.Networking;

namespace Slafight_Plugin_EXILED.API.Features;

public static class VoiceRecordingApi
{
    private static readonly Dictionary<string, VoiceRecording> RecordingsByKey = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, RecordingSession> SessionsByKey = new(StringComparer.OrdinalIgnoreCase);
    private static bool _registered;

    public static IReadOnlyDictionary<string, VoiceRecording> Recordings => RecordingsByKey;

    public static void RegisterEvents()
    {
        if (_registered)
            return;

        Exiled.Events.Handlers.Player.VoiceChatting += OnVoiceChatting;
        Exiled.Events.Handlers.Server.RestartingRound += ClearAll;
        _registered = true;
    }

    public static void UnregisterEvents()
    {
        if (!_registered)
            return;

        Exiled.Events.Handlers.Player.VoiceChatting -= OnVoiceChatting;
        Exiled.Events.Handlers.Server.RestartingRound -= ClearAll;
        ClearAll();
        _registered = false;
    }

    public static VoiceRecording StartAreaRecording(
        string key,
        Vector3 position,
        float radius,
        float maxDuration = 30f,
        IEnumerable<VoiceChatChannel>? channels = null,
        Func<Player, bool>? playerFilter = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Recording key cannot be empty.", nameof(key));

        StopRecording(key);

        var recording = new VoiceRecording(key);
        RecordingsByKey[key] = recording;
        SessionsByKey[key] = new RecordingSession(
            recording,
            position,
            Mathf.Max(0f, radius),
            playerFilter,
            channels?.ToArray() ?? []);

        if (maxDuration > 0f)
            Timing.CallDelayed(maxDuration, () => StopRecording(key));

        return recording;
    }

    public static bool StopRecording(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        if (!SessionsByKey.TryGetValue(key, out var session))
            return false;

        session.Recording.MarkCompleted();
        SessionsByKey.Remove(key);
        return true;
    }

    public static bool TryGetRecording(string key, out VoiceRecording recording)
    {
        recording = null;
        return !string.IsNullOrWhiteSpace(key) && RecordingsByKey.TryGetValue(key, out recording);
    }

    public static bool RemoveRecording(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        StopRecording(key);
        return RecordingsByKey.Remove(key);
    }

    public static void ClearAll()
    {
        SessionsByKey.Clear();
        RecordingsByKey.Clear();
    }

    public static CoroutineHandle Play(
        string key,
        Vector3 position,
        string? audioPlayerName = null,
        Transform? parent = null,
        bool isSpatial = true,
        float maxDistance = 10f,
        float minDistance = 0f,
        IEnumerable<Player>? targets = null,
        bool destroyOnEnd = true)
    {
        if (!TryGetRecording(key, out var recording))
            return default;

        return Play(recording, position, audioPlayerName, parent, isSpatial, maxDistance, minDistance, targets, destroyOnEnd);
    }

    public static CoroutineHandle Play(
        VoiceRecording recording,
        Vector3 position,
        string? audioPlayerName = null,
        Transform? parent = null,
        bool isSpatial = true,
        float maxDistance = 10f,
        float minDistance = 0f,
        IEnumerable<Player>? targets = null,
        bool destroyOnEnd = true)
    {
        if (recording == null || recording.FrameCount == 0)
            return default;

        audioPlayerName ??= $"VoiceRecording_{recording.Hash}";
        return Timing.RunCoroutine(PlayCoroutine(
            recording,
            audioPlayerName,
            position,
            parent,
            isSpatial,
            maxDistance,
            minDistance,
            targets,
            destroyOnEnd));
    }

    private static void OnVoiceChatting(VoiceChattingEventArgs ev)
    {
        if (ev?.Player == null || ev.VoiceMessage.Data == null || ev.VoiceMessage.DataLength <= 0)
            return;

        foreach (var session in SessionsByKey.Values.ToArray())
        {
            if (!session.ShouldCapture(ev))
                continue;

            session.Recording.AddFrame(ev.VoiceMessage);
        }
    }

    private static IEnumerator<float> PlayCoroutine(
        VoiceRecording recording,
        string audioPlayerName,
        Vector3 position,
        Transform? parent,
        bool isSpatial,
        float maxDistance,
        float minDistance,
        IEnumerable<Player>? targets,
        bool destroyOnEnd)
    {
        var playback = SpeakerApi.CreateLiveSpeaker(
            audioPlayerName,
            position,
            parent,
            speakerName: "Recording",
            isSpatial: isSpatial,
            maxDistance: maxDistance,
            minDistance: minDistance);

        var hubs = ResolveTargets(targets).ToArray();
        foreach (var frame in recording.GetFramesSnapshot())
        {
            if (frame.DelaySeconds > 0f)
                yield return Timing.WaitForSeconds(frame.DelaySeconds);

            playback.SetTransform(position, parent);
            playback.SendFrame(frame.Data, frame.DataLength, hubs);
        }

        if (destroyOnEnd)
            playback.DestroyAudioPlayer();
    }

    private static IEnumerable<ReferenceHub> ResolveTargets(IEnumerable<Player>? targets)
    {
        if (targets == null)
            return ReferenceHub.AllHubs;

        return targets
            .Where(player => player?.ReferenceHub != null)
            .Select(player => player.ReferenceHub);
    }

    private sealed class RecordingSession
    {
        private readonly Func<Player, bool>? _playerFilter;
        private readonly HashSet<VoiceChatChannel> _channels;

        public RecordingSession(
            VoiceRecording recording,
            Vector3 position,
            float radius,
            Func<Player, bool>? playerFilter,
            VoiceChatChannel[] channels)
        {
            Recording = recording;
            Position = position;
            Radius = radius;
            _playerFilter = playerFilter;
            _channels = channels.Length > 0 ? new HashSet<VoiceChatChannel>(channels) : [];
        }

        public VoiceRecording Recording { get; }
        public Vector3 Position { get; }
        public float Radius { get; }

        public bool ShouldCapture(VoiceChattingEventArgs ev)
        {
            if (ev.Player == null)
                return false;

            if (_channels.Count > 0 && !_channels.Contains(ev.VoiceMessage.Channel))
                return false;

            if (_playerFilter != null && !_playerFilter(ev.Player))
                return false;

            return Vector3.Distance(ev.Player.Position, Position) <= Radius;
        }
    }
}

public sealed class VoiceRecording
{
    private readonly List<VoiceRecordingFrame> _frames = [];
    private float _lastFrameTime;
    private string? _cachedHash;

    public VoiceRecording(string key)
    {
        Key = key;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public string Key { get; }
    public DateTime CreatedAtUtc { get; }
    public bool IsCompleted { get; private set; }
    public int FrameCount => _frames.Count;
    public float DurationSeconds { get; private set; }
    public string Hash => _cachedHash ??= ComputeHash();

    public IReadOnlyList<VoiceRecordingFrame> GetFramesSnapshot()
        => _frames.ToArray();

    internal void AddFrame(VoiceMessage message)
    {
        float now = Time.unscaledTime;
        float delay = _frames.Count == 0 ? 0f : Mathf.Max(0f, now - _lastFrameTime);
        _lastFrameTime = now;

        var data = new byte[message.DataLength];
        Buffer.BlockCopy(message.Data, 0, data, 0, message.DataLength);
        _frames.Add(new VoiceRecordingFrame(data, message.DataLength, delay));
        DurationSeconds += delay;
        _cachedHash = null;
    }

    internal void MarkCompleted()
        => IsCompleted = true;

    private string ComputeHash()
    {
        using var sha = SHA256.Create();
        var seed = Encoding.UTF8.GetBytes($"{Key}:{CreatedAtUtc.Ticks}:{FrameCount}:{DurationSeconds:F4}");
        sha.TransformBlock(seed, 0, seed.Length, null, 0);

        foreach (var frame in _frames)
        {
            var delay = BitConverter.GetBytes(frame.DelaySeconds);
            var length = BitConverter.GetBytes(frame.DataLength);
            sha.TransformBlock(delay, 0, delay.Length, null, 0);
            sha.TransformBlock(length, 0, length.Length, null, 0);
            sha.TransformBlock(frame.Data, 0, frame.DataLength, null, 0);
        }

        sha.TransformFinalBlock([], 0, 0);
        return BitConverter.ToString(sha.Hash).Replace("-", string.Empty).ToLowerInvariant();
    }
}

public readonly struct VoiceRecordingFrame
{
    public VoiceRecordingFrame(byte[] data, int dataLength, float delaySeconds)
    {
        Data = data;
        DataLength = dataLength;
        DelaySeconds = delaySeconds;
    }

    public byte[] Data { get; }
    public int DataLength { get; }
    public float DelaySeconds { get; }
}
