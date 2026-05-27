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
using VoiceChat.Codec;
using VoiceChat.Networking;
using VoiceChat.Playbacks;
using LabSpeakerToy = LabApi.Features.Wrappers.SpeakerToy;

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

    public static bool IsRecording(string key)
        => !string.IsNullOrWhiteSpace(key) && SessionsByKey.ContainsKey(key);

    public static bool RemoveRecording(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        StopRecording(key);
        return RecordingsByKey.Remove(key);
    }

    public static void ClearAll()
    {
        foreach (var session in SessionsByKey.Values)
            session.Recording.MarkCompleted();

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
        bool destroyOnEnd = true,
        float startupDelay = 0.5f,
        float destroyDelay = 0.75f)
    {
        if (!TryGetRecording(key, out var recording))
            return default;

        return Play(recording, position, audioPlayerName, parent, isSpatial, maxDistance, minDistance, targets, destroyOnEnd, startupDelay, destroyDelay);
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
        bool destroyOnEnd = true,
        float startupDelay = 0.5f,
        float destroyDelay = 0.75f)
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
            destroyOnEnd,
            startupDelay,
            destroyDelay));
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
        bool destroyOnEnd,
        float startupDelay,
        float destroyDelay)
    {
        if (startupDelay > 0f)
            yield return Timing.WaitForSeconds(startupDelay);

        SpeakerApi.PlaySamples(
            audioPlayerName,
            recording.GetSamplesSnapshot(),
            position,
            parent,
            isSpatial,
            maxDistance,
            minDistance,
            volume: 1f,
            loop: false,
            targets,
            destroyOnEnd);

        if (destroyOnEnd)
            yield return Timing.WaitForSeconds(recording.DurationSeconds + destroyDelay);
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
    private readonly List<float> _samples = [];
    private readonly OpusDecoder _decoder = new();
    private readonly float[] _decodeBuffer = new float[VoiceChatSettings.BufferLength];
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
    public int FrameCount { get; private set; }
    public int SampleCount => _samples.Count;
    public float DurationSeconds { get; private set; }
    public string Hash => _cachedHash ??= ComputeHash();

    public float[] GetSamplesSnapshot()
        => _samples.ToArray();

    internal void AddFrame(VoiceMessage message)
    {
        if (IsCompleted)
            return;

        float now = Time.unscaledTime;
        float delay = FrameCount == 0 ? 0f : Mathf.Max(0f, now - _lastFrameTime);
        _lastFrameTime = now;

        if (delay > 0f)
        {
            int silenceSamples = Mathf.RoundToInt(delay * VoiceChatSettings.SampleRate);
            if (silenceSamples > 0)
                _samples.AddRange(new float[silenceSamples]);
        }

        int decodedLength = _decoder.Decode(message.Data, message.DataLength, _decodeBuffer);
        if (decodedLength <= 0)
            return;

        for (int i = 0; i < decodedLength; i++)
            _samples.Add(_decodeBuffer[i]);

        FrameCount++;
        DurationSeconds = _samples.Count * VoiceChatSettings.SampleToDuartionRate;
        _cachedHash = null;
    }

    internal void MarkCompleted()
    {
        if (IsCompleted)
            return;

        IsCompleted = true;
        _decoder.Dispose();
    }

    private string ComputeHash()
    {
        using var sha = SHA256.Create();
        var seed = Encoding.UTF8.GetBytes($"{Key}:{CreatedAtUtc.Ticks}:{FrameCount}:{SampleCount}:{DurationSeconds:F4}");
        sha.TransformBlock(seed, 0, seed.Length, null, 0);

        foreach (var sample in _samples)
        {
            var bytes = BitConverter.GetBytes(sample);
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }

        sha.TransformFinalBlock([], 0, 0);
        return BitConverter.ToString(sha.Hash).Replace("-", string.Empty).ToLowerInvariant();
    }
}
