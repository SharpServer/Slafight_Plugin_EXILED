using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Exiled.API.Features;
using MEC;
using NVorbis;
using UnityEngine;
using VoiceChat;
using VoiceChat.Networking;
using VoiceChat.Playbacks;
using LabSpeakerToy = LabApi.Features.Wrappers.SpeakerToy;

namespace Slafight_Plugin_EXILED.API.Features;

public static class SpeakerApi
{
    private const int TargetSampleRate = VoiceChatSettings.SampleRate;
    private const int PacketSize = VoiceChatSettings.PacketSizePerChannel;
    private const float MinimumAudibleDistance = 1f;
    private static readonly Dictionary<string, CachedClip> ClipCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, List<Playback>> PlaybacksByName = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, List<LivePlayback>> LivePlaybacksByName = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<byte, CoroutineHandle> PlaybackStreams = new();
    private static readonly HashSet<byte> AllocatedControllerIds = [];

    public readonly struct Playback
    {
        public Playback(string audioPlayerName, string clipName, LabSpeakerToy speaker, byte controllerId, CoroutineHandle cleanupHandle = default)
        {
            AudioPlayerName = audioPlayerName;
            ClipName = clipName;
            Speaker = speaker;
            ControllerId = controllerId;
            CleanupHandle = cleanupHandle;
        }

        public string AudioPlayerName { get; }
        public string ClipName { get; }
        public LabSpeakerToy Speaker { get; }
        public byte ControllerId { get; }
        public CoroutineHandle CleanupHandle { get; }
        public bool IsValid => Speaker != null && !Speaker.IsDestroyed && ControllerId != 0;

        public bool Stop()
            => SpeakerApi.Stop(this);

        public bool DestroyAudioPlayer()
            => Stop();

        public void SetTransform(Vector3 position, Transform? parent = null)
            => SpeakerApi.SetTransform(this, position, parent);
    }

    public readonly struct LivePlayback
    {
        public LivePlayback(string audioPlayerName, LabSpeakerToy speaker, byte controllerId)
        {
            AudioPlayerName = audioPlayerName;
            Speaker = speaker;
            ControllerId = controllerId;
        }

        public string AudioPlayerName { get; }
        public LabSpeakerToy Speaker { get; }
        public byte ControllerId { get; }
        public bool IsValid => Speaker != null && !Speaker.IsDestroyed && ControllerId != 0;

        public bool DestroyAudioPlayer()
            => SpeakerApi.DestroyLiveSpeaker(this);

        public void SetTransform(Vector3 position, Transform? parent = null)
            => SpeakerApi.SetTransform(this, position, parent);

        public int SendFrame(byte[] data, int dataLength, IEnumerable<ReferenceHub> targets)
            => SpeakerApi.SendAudioFrame(this, data, dataLength, targets);
    }

    private sealed class CachedClip
    {
        public CachedClip(string name, float[] samples)
        {
            Name = name;
            Samples = samples;
            Duration = samples.Length * VoiceChatSettings.SampleToDuartionRate;
        }

        public string Name { get; }
        public float[] Samples { get; }
        public float Duration { get; }
    }

    public static string AudioDirectory => Plugin.Singleton.Config.AudioReferences;

    public static LivePlayback CreateLiveSpeaker(
        string audioPlayerName,
        Vector3 position,
        Transform? parent = null,
        string? speakerName = null,
        bool isSpatial = true,
        float maxDistance = 5f,
        float minDistance = MinimumAudibleDistance,
        float volume = 1f)
    {
        if (string.IsNullOrWhiteSpace(audioPlayerName))
            throw new ArgumentException("Audio player name cannot be empty.", nameof(audioPlayerName));

        var speaker = CreateSpeaker(position, parent, isSpatial, maxDistance, minDistance, volume);
        var playback = new LivePlayback(audioPlayerName, speaker, speaker.ControllerId);
        AddLivePlayback(playback);
        return playback;
    }

    public static Playback Play(
        string fileName,
        string audioPlayerName,
        Vector3 position,
        bool destroyOnEnd = false,
        Transform? parent = null,
        bool isSpatial = false,
        float maxDistance = 5f,
        float minDistance = 5f,
        bool loadClip = true,
        string? speakerName = null,
        string? clipName = null)
    {
        if (string.IsNullOrWhiteSpace(audioPlayerName))
            throw new ArgumentException("Audio player name cannot be empty.", nameof(audioPlayerName));

        return PlayCore(fileName, audioPlayerName, position, parent, isSpatial, maxDistance, minDistance, loadClip, clipName, loop: false, destroyOnEnd);
    }

    public static Playback PlayLoop(
        string fileName,
        string audioPlayerName,
        Vector3 position,
        Transform? parent = null,
        bool isSpatial = false,
        float maxDistance = 5f,
        float minDistance = 5f,
        bool loadClip = true,
        string? speakerName = null,
        string? clipName = null,
        bool restartIfAlreadyPlaying = true)
    {
        if (restartIfAlreadyPlaying)
            TryDestroy(audioPlayerName);

        return PlayCore(fileName, audioPlayerName, position, parent, isSpatial, maxDistance, minDistance, loadClip, clipName, loop: true, destroyOnEnd: false);
    }

    private static Playback PlayCore(
        string fileName,
        string audioPlayerName,
        Vector3 position,
        Transform? parent,
        bool isSpatial,
        float maxDistance,
        float minDistance,
        bool loadClip,
        string? clipName,
        bool loop,
        bool destroyOnEnd)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Audio file name cannot be empty.", nameof(fileName));

        if (string.IsNullOrWhiteSpace(audioPlayerName))
            throw new ArgumentException("Audio player name cannot be empty.", nameof(audioPlayerName));

        clipName ??= fileName;
        if (loadClip || !ClipCache.ContainsKey(clipName))
            LoadClip(fileName, clipName);

        if (!ClipCache.TryGetValue(clipName, out var clip))
            throw new InvalidOperationException($"Audio clip is not loaded: {clipName}");

        var speaker = CreateSpeaker(position, parent, isSpatial, maxDistance, minDistance, 1f);
        speaker.Play(clip.Samples, queue: false, loop);
        var playback = new Playback(audioPlayerName, clipName, speaker, speaker.ControllerId);
        AddPlayback(playback);

        if (destroyOnEnd && !loop)
        {
            Timing.CallDelayed(clip.Duration + 0.75f, () => Stop(playback));
        }

        return playback;
    }

    public static Playback PlaySamples(
        string audioPlayerName,
        float[] samples,
        Vector3 position,
        Transform? parent = null,
        bool isSpatial = true,
        float maxDistance = 10f,
        float minDistance = MinimumAudibleDistance,
        float volume = 1f,
        bool loop = false,
        IEnumerable<Player>? targets = null,
        bool destroyOnEnd = true)
    {
        if (string.IsNullOrWhiteSpace(audioPlayerName))
            throw new ArgumentException("Audio player name cannot be empty.", nameof(audioPlayerName));

        if (samples == null || samples.Length == 0)
            throw new ArgumentException("Samples cannot be empty.", nameof(samples));

        var speaker = CreateSpeaker(position, parent, isSpatial, maxDistance, minDistance, volume);
        var targetIds = targets?
            .Where(player => !string.IsNullOrEmpty(player.UserId))
            .Select(player => player.UserId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        speaker.ValidPlayers = targetIds == null ? null : player => player != null && targetIds.Contains(player.UserId);
        speaker.Play(samples, queue: false, loop);
        var playback = new Playback(audioPlayerName, audioPlayerName, speaker, speaker.ControllerId);
        AddPlayback(playback);

        if (destroyOnEnd && !loop)
        {
            float duration = samples.Length * VoiceChatSettings.SampleToDuartionRate;
            Timing.CallDelayed(duration + 0.75f, () => Stop(playback));
        }

        return playback;
    }

    public static void LoadClip(string fileName, string? clipName = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Audio file name cannot be empty.", nameof(fileName));

        clipName ??= fileName;
        if (ClipCache.ContainsKey(clipName))
            return;

        var fullPath = Path.Combine(AudioDirectory, fileName);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Audio file not found: {fullPath}", fullPath);

        using var reader = new VorbisReader(fullPath);
        var samples = new float[reader.TotalSamples * reader.Channels];
        reader.ReadSamples(samples, 0, samples.Length);
        ClipCache[clipName] = new CachedClip(clipName, ConvertToMono48k(samples, reader.SampleRate, reader.Channels));
    }

    public static bool Stop(Playback playback)
    {
        if (!playback.IsValid)
            return false;

        if (PlaybackStreams.TryGetValue(playback.ControllerId, out var stream) && stream.IsRunning)
            Timing.KillCoroutines(stream);

        PlaybackStreams.Remove(playback.ControllerId);
        playback.Speaker.Stop();
        playback.Speaker.Destroy();
        AllocatedControllerIds.Remove(playback.ControllerId);
        RemovePlayback(playback);
        return true;
    }

    public static bool StopClip(string audioPlayerName, string clipName)
    {
        if (string.IsNullOrWhiteSpace(audioPlayerName) || string.IsNullOrWhiteSpace(clipName))
            return false;

        if (!PlaybacksByName.TryGetValue(audioPlayerName, out var playbacks))
            return false;

        var targets = playbacks.Where(p => string.Equals(p.ClipName, clipName, StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (var playback in targets)
            Stop(playback);

        return targets.Length > 0;
    }

    public static int StopClip(string clipName)
    {
        if (string.IsNullOrWhiteSpace(clipName))
            return 0;

        int stopped = 0;
        foreach (var playback in PlaybacksByName.Values.SelectMany(p => p).Where(p => string.Equals(p.ClipName, clipName, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            if (Stop(playback))
                stopped++;
        }

        return stopped;
    }

    public static int StopClips(params Playback[]? playbacks)
    {
        if (playbacks == null || playbacks.Length == 0)
            return 0;

        int stopped = 0;
        foreach (var playback in playbacks)
        {
            if (Stop(playback))
                stopped++;
        }

        return stopped;
    }

    public static int StopClips(string audioPlayerName, params string[]? clipNames)
    {
        if (string.IsNullOrWhiteSpace(audioPlayerName) || clipNames == null || clipNames.Length == 0)
            return 0;

        int stopped = 0;
        foreach (var clipName in clipNames)
        {
            if (StopClip(audioPlayerName, clipName))
                stopped++;
        }

        return stopped;
    }

    public static bool TryDestroy(string audioPlayerName)
    {
        if (string.IsNullOrWhiteSpace(audioPlayerName) || !PlaybacksByName.TryGetValue(audioPlayerName, out var playbacks))
            return false;

        foreach (var playback in playbacks.ToArray())
            Stop(playback);

        return true;
    }

    public static int DestroyAll()
    {
        var playbacks = PlaybacksByName.Values.SelectMany(p => p).ToArray();
        foreach (var playback in playbacks)
            Stop(playback);

        var livePlaybacks = LivePlaybacksByName.Values.SelectMany(p => p).ToArray();
        foreach (var playback in livePlaybacks)
            DestroyLiveSpeaker(playback);

        PlaybacksByName.Clear();
        LivePlaybacksByName.Clear();
        AllocatedControllerIds.Clear();
        return playbacks.Length + livePlaybacks.Length;
    }

    public static void SetTransform(Playback playback, Vector3 position, Transform? parent = null)
    {
        if (!playback.IsValid)
            return;

        SetTransform(playback.Speaker, position, parent);
    }

    public static void SetTransform(LivePlayback playback, Vector3 position, Transform? parent = null)
    {
        if (!playback.IsValid)
            return;

        SetTransform(playback.Speaker, position, parent);
    }

    public static bool DestroyLiveSpeaker(LivePlayback playback)
    {
        if (!playback.IsValid)
            return false;

        playback.Speaker.Destroy();
        AllocatedControllerIds.Remove(playback.ControllerId);
        RemoveLivePlayback(playback);
        return true;
    }

    public static int SendAudioFrame(LivePlayback playback, byte[]? data, int dataLength, IEnumerable<ReferenceHub>? targets)
    {
        if (!playback.IsValid || data == null || dataLength <= 0 || dataLength > data.Length || targets == null)
        {
            Log.Debug($"[SpeakerApi] SendAudioFrame: Validation failed. Playback Valid: {playback.IsValid}, Data null: {data == null}, DataLength: {dataLength}, targets null: {targets == null}");
            return 0;
        }

        var audioMessage = new AudioMessage(playback.ControllerId, data, dataLength);
        int sent = 0;
        foreach (var target in targets)
        {
            if (target?.connectionToClient == null)
                continue;

            target.connectionToClient.Send(audioMessage);
            sent++;
        }

        return sent;
    }

    public static int SendAudioFrame(string audioPlayerName, byte[]? data, int dataLength, IEnumerable<ReferenceHub>? targets)
    {
        if (string.IsNullOrWhiteSpace(audioPlayerName) || data == null || dataLength <= 0 || dataLength > data.Length || targets == null)
            return 0;

        if (!LivePlaybacksByName.TryGetValue(audioPlayerName, out var playbacks))
            return 0;

        int sent = 0;
        foreach (var playback in playbacks.ToArray())
        {
            if (!playback.IsValid)
            {
                RemoveLivePlayback(playback);
                continue;
            }

            sent += SendAudioFrame(playback, data, dataLength, targets);
        }

        return sent;
    }

    public static IEnumerable<string> GetAudioPlayerNames()
        => PlaybacksByName.Keys.ToArray();

    private static LabSpeakerToy CreateSpeaker(Vector3 position, Transform? parent, bool isSpatial, float maxDistance, float minDistance, float volume)
    {
        minDistance = Mathf.Max(MinimumAudibleDistance, minDistance);
        maxDistance = Mathf.Max(minDistance, maxDistance);

        var speaker = LabSpeakerToy.Create(parent ? Vector3.zero : position, Quaternion.identity, Vector3.one, parent, networkSpawn: false);
        speaker.ControllerId = AllocateControllerId();
        speaker.IsSpatial = isSpatial;
        speaker.MaxDistance = maxDistance;
        speaker.MinDistance = minDistance;
        speaker.Volume = volume;
        speaker.ValidPlayers = null;

        // Force SyncVar values on the base NetworkBehaviour so they are synchronized to clients during Spawn
        speaker.Base.NetworkControllerId = speaker.ControllerId;
        speaker.Base.NetworkIsSpatial = isSpatial;
        speaker.Base.NetworkMaxDistance = maxDistance;
        speaker.Base.NetworkMinDistance = minDistance;
        speaker.Base.NetworkVolume = volume;

        if (parent == null)
        {
            speaker.Base.NetworkPosition = position;
        }

        speaker.Spawn();
        return speaker;
    }

    private static void SetTransform(LabSpeakerToy speaker, Vector3 position, Transform? parent)
    {
        if (parent)
        {
            speaker.Transform.SetParent(parent);
            speaker.Transform.localPosition = Vector3.zero;
            speaker.Transform.localRotation = Quaternion.identity;
            return;
        }

        speaker.Base.NetworkPosition = position;
        speaker.Position = position;
    }

    private static byte AllocateControllerId()
    {
        var used = new HashSet<byte>(AllocatedControllerIds);

        foreach (var speaker in LabSpeakerToy.List)
            used.Add(speaker.ControllerId);

        foreach (var playback in SpeakerToyPlaybackBase.AllInstances)
            used.Add(playback.ControllerId);

        for (byte id = 1; id < byte.MaxValue; id++)
        {
            if (used.Contains(id))
                continue;

            AllocatedControllerIds.Add(id);
            return id;
        }

        throw new InvalidOperationException("No available SpeakerToy controller IDs.");
    }

    private static void AddPlayback(Playback playback)
    {
        if (!PlaybacksByName.TryGetValue(playback.AudioPlayerName, out var list))
        {
            list = [];
            PlaybacksByName[playback.AudioPlayerName] = list;
        }

        list.Add(playback);
    }

    private static void RemovePlayback(Playback playback)
    {
        if (!PlaybacksByName.TryGetValue(playback.AudioPlayerName, out var list))
            return;

        list.RemoveAll(p => p.ControllerId == playback.ControllerId);
        if (list.Count == 0)
            PlaybacksByName.Remove(playback.AudioPlayerName);
    }

    private static void AddLivePlayback(LivePlayback playback)
    {
        if (!LivePlaybacksByName.TryGetValue(playback.AudioPlayerName, out var list))
        {
            list = [];
            LivePlaybacksByName[playback.AudioPlayerName] = list;
        }

        list.Add(playback);
    }

    private static void RemoveLivePlayback(LivePlayback playback)
    {
        if (!LivePlaybacksByName.TryGetValue(playback.AudioPlayerName, out var list))
            return;

        list.RemoveAll(p => p.ControllerId == playback.ControllerId);
        if (list.Count == 0)
            LivePlaybacksByName.Remove(playback.AudioPlayerName);
    }

    private static float[] ConvertToMono48k(float[]? input, int sampleRate, int channels)
    {
        if (input == null || input.Length == 0)
            return [];

        channels = Math.Max(1, channels);
        int frameCount = input.Length / channels;
        var mono = new float[frameCount];
        for (int frame = 0; frame < frameCount; frame++)
        {
            float sample = 0f;
            int offset = frame * channels;
            for (int channel = 0; channel < channels; channel++)
                sample += input[offset + channel];

            mono[frame] = sample / channels;
        }

        if (sampleRate <= 0 || sampleRate == TargetSampleRate)
            return mono;

        int outputLength = Mathf.Max(1, Mathf.RoundToInt(mono.Length * (TargetSampleRate / (float)sampleRate)));
        var output = new float[outputLength];
        float ratio = (mono.Length - 1) / (float)Math.Max(1, outputLength - 1);
        for (int i = 0; i < outputLength; i++)
        {
            float source = i * ratio;
            int left = Mathf.FloorToInt(source);
            int right = Mathf.Min(left + 1, mono.Length - 1);
            output[i] = Mathf.Lerp(mono[left], mono[right], source - left);
        }

        return output;
    }
}
