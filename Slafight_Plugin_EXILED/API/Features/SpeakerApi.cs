using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Exiled.API.Features;
using MEC;
using UnityEngine;
using VoiceChat;
using VoiceChat.Networking;
using VoiceChat.Playbacks;
using LabSpeakerToy = LabApi.Features.Wrappers.SpeakerToy;

namespace Slafight_Plugin_EXILED.API.Features;

public static class SpeakerApi
{
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
        public float Volume => IsValid ? Speaker.Volume : 0f;

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
        public float Volume => IsValid ? Speaker.Volume : 0f;

        public bool DestroyAudioPlayer()
            => DestroyLiveSpeaker(this);

        public void SetTransform(Vector3 position, Transform? parent = null)
            => SpeakerApi.SetTransform(this, position, parent);

        public void SetVolume(float volume)
            => SpeakerApi.SetVolume(this, volume);

        public void SetListeners(Predicate<Player>? listeners)
            => SpeakerApi.SetListeners(this, listeners);

        public int SendFrame(byte[] data, int dataLength, IEnumerable<ReferenceHub> targets)
            => SendAudioFrame(this, data, dataLength, targets);
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
        float volume = 1f,
        Predicate<Player>? listeners = null)
    {
        if (string.IsNullOrWhiteSpace(audioPlayerName))
            throw new ArgumentException("Audio player name cannot be empty.", nameof(audioPlayerName));

        LabSpeakerToy? speaker = null;
        var playback = default(LivePlayback);
        try
        {
            speaker = CreateSpeaker(position, parent, isSpatial, maxDistance, minDistance, volume, listeners);
            playback = new LivePlayback(audioPlayerName, speaker, speaker.ControllerId);
            AddLivePlayback(playback);
            return playback;
        }
        catch (Exception ex)
        {
            Log.Warn($"[SpeakerApi] CreateLiveSpeaker failed for {audioPlayerName}: {ex.Message}");
            if (speaker != null)
            {
                var failedPlayback = playback.ControllerId == 0
                    ? new LivePlayback(audioPlayerName, speaker, speaker.ControllerId)
                    : playback;
                DestroyLiveSpeaker(failedPlayback);
            }

            return default;
        }
    }

    public static Playback Play(
        string fileName,
        string audioPlayerName,
        Vector3 position,
        bool destroyOnEnd = false,
        Transform? parent = null,
        bool isSpatial = false,
        float maxDistance = 5f,
        float minDistance = 0.1f,
        bool loadClip = true,
        string? speakerName = null,
        string? clipName = null,
        float volume = 1f,
        Predicate<Player>? listeners = null)
    {
        if (string.IsNullOrWhiteSpace(audioPlayerName))
            throw new ArgumentException("Audio player name cannot be empty.", nameof(audioPlayerName));

        return PlayCore(fileName, audioPlayerName, position, parent, isSpatial, maxDistance, minDistance, volume, listeners, loadClip, clipName, loop: false, destroyOnEnd);
    }

    public static Playback PlayLoop(
        string fileName,
        string audioPlayerName,
        Vector3 position,
        Transform? parent = null,
        bool isSpatial = false,
        float maxDistance = 5f,
        float minDistance = 0.1f,
        bool loadClip = true,
        string? speakerName = null,
        string? clipName = null,
        bool restartIfAlreadyPlaying = true,
        float volume = 1f,
        Predicate<Player>? listeners = null)
    {
        if (restartIfAlreadyPlaying)
            TryDestroy(audioPlayerName);

        return PlayCore(fileName, audioPlayerName, position, parent, isSpatial, maxDistance, minDistance, volume, listeners, loadClip, clipName, loop: true, destroyOnEnd: false);
    }

    private static Playback PlayCore(
        string fileName,
        string audioPlayerName,
        Vector3 position,
        Transform? parent,
        bool isSpatial,
        float maxDistance,
        float minDistance,
        float volume,
        Predicate<Player>? listeners,
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
        if ((loadClip || !ClipCache.ContainsKey(clipName)) && !TryLoadClipCore(fileName, clipName))
            return default;

        if (!ClipCache.TryGetValue(clipName, out var clip))
        {
            Log.Warn($"[SpeakerApi] Audio clip is not loaded: {clipName}");
            return default;
        }

        LabSpeakerToy? speaker = null;
        var playback = default(Playback);
        try
        {
            speaker = CreateSpeaker(position, parent, isSpatial, maxDistance, minDistance, volume, listeners);
            speaker.Play(clip.Samples, queue: false, loop);
            playback = new Playback(audioPlayerName, clipName, speaker, speaker.ControllerId);
            AddPlayback(playback);

            if (destroyOnEnd && !loop)
                Timing.CallDelayed(clip.Duration + 0.75f, () => Stop(playback));

            return playback;
        }
        catch (Exception ex)
        {
            Log.Warn($"[SpeakerApi] Play failed for {audioPlayerName}/{clipName}: {ex.Message}");
            if (speaker != null)
            {
                var failedPlayback = playback.ControllerId == 0
                    ? new Playback(audioPlayerName, clipName, speaker, speaker.ControllerId)
                    : playback;
                Stop(failedPlayback);
            }

            return default;
        }
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
        bool destroyOnEnd = true,
        Predicate<Player>? listeners = null)
    {
        if (string.IsNullOrWhiteSpace(audioPlayerName))
            throw new ArgumentException("Audio player name cannot be empty.", nameof(audioPlayerName));

        if (samples == null || samples.Length == 0)
            throw new ArgumentException("Samples cannot be empty.", nameof(samples));

        var targetIds = targets?
            .Where(player => player?.ReferenceHub != null && !string.IsNullOrEmpty(player.UserId))
            .Select(player => player.UserId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (targetIds != null)
        {
            var existingListeners = listeners;
            listeners = player =>
                targetIds.Contains(player.UserId) &&
                (existingListeners == null || existingListeners(player));
        }

        LabSpeakerToy? speaker = null;
        var playback = default(Playback);
        try
        {
            speaker = CreateSpeaker(position, parent, isSpatial, maxDistance, minDistance, volume, listeners);
            speaker.Play(samples, queue: false, loop);
            playback = new Playback(audioPlayerName, audioPlayerName, speaker, speaker.ControllerId);
            AddPlayback(playback);

            if (destroyOnEnd && !loop)
            {
                float duration = samples.Length * VoiceChatSettings.SampleToDuartionRate;
                Timing.CallDelayed(duration + 0.75f, () => Stop(playback));
            }

            return playback;
        }
        catch (Exception ex)
        {
            Log.Warn($"[SpeakerApi] PlaySamples failed for {audioPlayerName}: {ex.Message}");
            if (speaker != null)
            {
                var failedPlayback = playback.ControllerId == 0
                    ? new Playback(audioPlayerName, audioPlayerName, speaker, speaker.ControllerId)
                    : playback;
                Stop(failedPlayback);
            }

            return default;
        }
    }

    /// <summary>プリロード済みクリップの再生時間(秒)。未ロードなら 0。</summary>
    public static float GetClipDuration(string clipName)
    {
        if (string.IsNullOrWhiteSpace(clipName))
            return 0f;

        return ClipCache.TryGetValue(clipName, out var clip) ? clip.Duration : 0f;
    }

    public static void LoadClip(string fileName, string? clipName = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Audio file name cannot be empty.", nameof(fileName));

        clipName ??= fileName;
        TryLoadClipCore(fileName, clipName);
    }

    public static bool TryLoadClip(string fileName, string? clipName = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        clipName ??= fileName;
        return TryLoadClipCore(fileName, clipName);
    }

    private static bool TryLoadClipCore(string fileName, string clipName)
    {
        if (ClipCache.ContainsKey(clipName))
            return true;

        try
        {
            var fullPath = Path.Combine(AudioDirectory, fileName);
            if (!File.Exists(fullPath))
            {
                Log.Warn($"[SpeakerApi] Audio file not found: {fullPath}");
                return false;
            }

            var samples = FfmpegAudioDecoder.DecodeToMono48k(fullPath);
            if (samples == null || samples.Length == 0)
            {
                Log.Warn($"[SpeakerApi] Audio file produced no samples: {fullPath}");
                return false;
            }

            ClipCache[clipName] = new CachedClip(clipName, samples);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn($"[SpeakerApi] Failed to load audio clip '{fileName}' as '{clipName}': {ex.Message}");
            return false;
        }
    }

    public static bool Stop(Playback playback)
    {
        if (playback.ControllerId == 0)
            return false;

        var trackedBeforeStop = IsPlaybackTracked(playback);
        var ownsController = trackedBeforeStop || playback.IsValid;
        var removedStream = false;
        if (ownsController && PlaybackStreams.TryGetValue(playback.ControllerId, out var stream) && stream.IsRunning)
        {
            Timing.KillCoroutines(stream);
            removedStream = true;
        }

        if (ownsController)
            PlaybackStreams.Remove(playback.ControllerId);

        var destroyed = false;
        try
        {
            if (playback.Speaker != null && !playback.Speaker.IsDestroyed)
            {
                playback.Speaker.Base.netIdentity.RemoveShowState();
                playback.Speaker.Stop();
                playback.Speaker.Destroy();
                destroyed = true;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"[SpeakerApi] Stop failed for {playback.AudioPlayerName}/{playback.ControllerId}: {ex.Message}");
        }

        var removedPlayback = RemovePlayback(playback);
        var releasedId = !IsControllerTracked(playback.ControllerId) && AllocatedControllerIds.Remove(playback.ControllerId);
        return destroyed || releasedId || removedPlayback || removedStream;
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
        if (string.IsNullOrWhiteSpace(audioPlayerName))
            return false;

        var destroyed = false;
        if (PlaybacksByName.TryGetValue(audioPlayerName, out var playbacks))
        {
            foreach (var playback in playbacks.ToArray())
                destroyed |= Stop(playback);
        }

        if (LivePlaybacksByName.TryGetValue(audioPlayerName, out var livePlaybacks))
        {
            foreach (var playback in livePlaybacks.ToArray())
                destroyed |= DestroyLiveSpeaker(playback);
        }

        return destroyed;
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
        PlaybackStreams.Clear();
        AllocatedControllerIds.Clear();
        return playbacks.Length + livePlaybacks.Length;
    }

    public static int PruneInvalid()
    {
        var removed = 0;

        foreach (var playback in PlaybacksByName.Values.SelectMany(p => p).Where(p => !p.IsValid).ToArray())
        {
            if (Stop(playback))
                removed++;
        }

        foreach (var playback in LivePlaybacksByName.Values.SelectMany(p => p).Where(p => !p.IsValid).ToArray())
        {
            if (DestroyLiveSpeaker(playback))
                removed++;
        }

        return removed;
    }

    public static void SetTransform(Playback playback, Vector3 position, Transform? parent = null)
    {
        if (!playback.IsValid)
            return;

        try
        {
            SetTransform(playback.Speaker, position, parent);
        }
        catch (Exception ex)
        {
            Log.Warn($"[SpeakerApi] SetTransform failed for {playback.AudioPlayerName}/{playback.ControllerId}: {ex.Message}");
        }
    }

    public static void SetTransform(LivePlayback playback, Vector3 position, Transform? parent = null)
    {
        if (!playback.IsValid)
            return;

        try
        {
            SetTransform(playback.Speaker, position, parent);
        }
        catch (Exception ex)
        {
            Log.Warn($"[SpeakerApi] SetTransform failed for {playback.AudioPlayerName}/{playback.ControllerId}: {ex.Message}");
        }
    }

    public static void SetVolume(Playback playback, float volume)
    {
        if (playback.IsValid)
        {
            try
            {
                SetVolume(playback.Speaker, volume);
            }
            catch (Exception ex)
            {
                Log.Warn($"[SpeakerApi] SetVolume failed for {playback.AudioPlayerName}/{playback.ControllerId}: {ex.Message}");
            }
        }
    }

    public static void SetVolume(LivePlayback playback, float volume)
    {
        if (playback.IsValid)
        {
            try
            {
                SetVolume(playback.Speaker, volume);
            }
            catch (Exception ex)
            {
                Log.Warn($"[SpeakerApi] SetVolume failed for {playback.AudioPlayerName}/{playback.ControllerId}: {ex.Message}");
            }
        }
    }

    public static void SetListeners(Playback playback, Predicate<Player>? listeners)
    {
        if (playback.IsValid)
        {
            try
            {
                ApplyListeners(playback.Speaker, listeners);
            }
            catch (Exception ex)
            {
                Log.Warn($"[SpeakerApi] SetListeners failed for {playback.AudioPlayerName}/{playback.ControllerId}: {ex.Message}");
            }
        }
    }

    public static void SetListeners(LivePlayback playback, Predicate<Player>? listeners)
    {
        if (playback.IsValid)
        {
            try
            {
                ApplyListeners(playback.Speaker, listeners);
            }
            catch (Exception ex)
            {
                Log.Warn($"[SpeakerApi] SetListeners failed for {playback.AudioPlayerName}/{playback.ControllerId}: {ex.Message}");
            }
        }
    }

    public static bool DestroyLiveSpeaker(LivePlayback playback)
    {
        if (playback.ControllerId == 0)
            return false;

        var trackedBeforeDestroy = IsLivePlaybackTracked(playback);
        var ownsController = trackedBeforeDestroy || playback.IsValid;
        var destroyed = false;
        try
        {
            if (playback.Speaker != null && !playback.Speaker.IsDestroyed)
            {
                playback.Speaker.Base.netIdentity.RemoveShowState();
                playback.Speaker.Destroy();
                destroyed = true;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"[SpeakerApi] DestroyLiveSpeaker failed for {playback.AudioPlayerName}/{playback.ControllerId}: {ex.Message}");
        }

        var removedPlayback = RemoveLivePlayback(playback);
        var releasedId = ownsController && !IsControllerTracked(playback.ControllerId) && AllocatedControllerIds.Remove(playback.ControllerId);
        return destroyed || releasedId || removedPlayback;
    }

    public static int SendAudioFrame(LivePlayback playback, byte[]? data, int dataLength, IEnumerable<ReferenceHub>? targets)
    {
        if (!playback.IsValid)
        {
            DestroyLiveSpeaker(playback);
            return 0;
        }

        if (data == null || dataLength <= 0 || dataLength > data.Length || targets == null)
        {
            Log.Debug($"[SpeakerApi] SendAudioFrame: Validation failed. Data null: {data == null}, DataLength: {dataLength}, targets null: {targets == null}");
            return 0;
        }

        var audioMessage = new AudioMessage(playback.ControllerId, data, dataLength);
        int sent = 0;
        foreach (var target in targets)
        {
            try
            {
                if (target?.connectionToClient == null)
                    continue;

                var validPlayers = playback.Speaker.ValidPlayers;
                if (validPlayers != null)
                {
                    var labPlayer = LabApi.Features.Wrappers.Player.Get(target);
                    if (labPlayer == null || !validPlayers(labPlayer))
                        continue;
                }

                target.connectionToClient.Send(audioMessage);
                sent++;
            }
            catch (Exception ex)
            {
                Log.Warn($"[SpeakerApi] SendAudioFrame failed for controller {playback.ControllerId}: {ex.Message}");
            }
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
        => PlaybacksByName.Keys.Concat(LivePlaybacksByName.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private static LabSpeakerToy CreateSpeaker(
        Vector3 position,
        Transform? parent,
        bool isSpatial,
        float maxDistance,
        float minDistance,
        float volume,
        Predicate<Player>? listeners)
    {
        minDistance = Mathf.Max(MinimumAudibleDistance, minDistance);
        maxDistance = Mathf.Max(minDistance, maxDistance);

        LabSpeakerToy? speaker = null;
        var controllerId = AllocateControllerId();
        try
        {
            speaker = LabSpeakerToy.Create(parent ? Vector3.zero : position, Quaternion.identity, Vector3.one, parent, networkSpawn: false);
            speaker.ControllerId = controllerId;
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
            ApplyListeners(speaker, listeners);
            return speaker;
        }
        catch
        {
            AllocatedControllerIds.Remove(controllerId);
            try
            {
                if (speaker != null && !speaker.IsDestroyed)
                    speaker.Destroy();
            }
            catch
            {
                // ignored
            }

            throw;
        }
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

    private static void SetVolume(LabSpeakerToy speaker, float volume)
    {
        speaker.Volume = volume;
        speaker.Base.NetworkVolume = volume;
    }

    private static void ApplyListeners(LabSpeakerToy speaker, Predicate<Player>? listeners)
    {
        if (speaker == null)
            return;

        if (listeners == null)
        {
            speaker.ValidPlayers = null;
            var identity = speaker.Base.netIdentity;
            if (identity.GetShowState() != null)
            {
                identity.RemoveShowState();
                foreach (var player in Player.List)
                    player?.ShowNetworkIdentity(identity);
            }

            return;
        }

        speaker.ValidPlayers = labPlayer =>
        {
            var player = labPlayer == null ? null : Player.Get(labPlayer.ReferenceHub);
            return player != null && MatchesListener(listeners, player);
        };

        speaker.Base.netIdentity.InitShowState(new NetworkShowState
        {
            VisibilityPredicate = player => MatchesListener(listeners, player),
        });
    }

    private static bool MatchesListener(Predicate<Player> listeners, Player player)
    {
        try
        {
            return listeners(player);
        }
        catch (Exception ex)
        {
            Log.Warn($"[SpeakerApi] Listener predicate failed: {ex.Message}");
            return false;
        }
    }

    private static byte AllocateControllerId()
    {
        PruneInvalid();
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

    private static bool RemovePlayback(Playback playback)
    {
        if (!PlaybacksByName.TryGetValue(playback.AudioPlayerName, out var list))
            return false;

        var removed = list.RemoveAll(p => p.ControllerId == playback.ControllerId) > 0;
        if (list.Count == 0)
            PlaybacksByName.Remove(playback.AudioPlayerName);

        return removed;
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

    private static bool RemoveLivePlayback(LivePlayback playback)
    {
        if (!LivePlaybacksByName.TryGetValue(playback.AudioPlayerName, out var list))
            return false;

        var removed = list.RemoveAll(p => p.ControllerId == playback.ControllerId) > 0;
        if (list.Count == 0)
            LivePlaybacksByName.Remove(playback.AudioPlayerName);

        return removed;
    }

    private static bool IsPlaybackTracked(Playback playback)
        => PlaybacksByName.TryGetValue(playback.AudioPlayerName, out var list) &&
           list.Any(p => p.ControllerId == playback.ControllerId);

    private static bool IsLivePlaybackTracked(LivePlayback playback)
        => LivePlaybacksByName.TryGetValue(playback.AudioPlayerName, out var list) &&
           list.Any(p => p.ControllerId == playback.ControllerId);

    private static bool IsControllerTracked(byte controllerId)
        => PlaybacksByName.Values.SelectMany(p => p).Any(p => p.ControllerId == controllerId) ||
           LivePlaybacksByName.Values.SelectMany(p => p).Any(p => p.ControllerId == controllerId);

}
