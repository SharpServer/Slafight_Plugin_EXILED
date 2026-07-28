using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using InventorySystem.Items.Keycards;
using MEC;
using UnityEngine;
using VoiceChat;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// High-level options for synchronized video and spatial audio playback on a
/// Chaos Keycard.
/// </summary>
public sealed class SnakeMediaOptions
{
    public SnakeImageOptions Image { get; set; } = new();
    public bool IsSpatial { get; set; } = true;
    public bool FollowOwner { get; set; } = true;
    public float MaxDistance { get; set; } = 12f;
    public float MinDistance { get; set; } = 1f;
    public float Volume { get; set; } = 1f;
    public string? AudioPlayerName { get; set; }
    public Predicate<Player>? Listeners { get; set; }

    internal SnakeMediaOptions Snapshot()
    {
        if (MaxDistance <= 0f)
            throw new ArgumentOutOfRangeException(nameof(MaxDistance));
        if (MinDistance <= 0f)
            throw new ArgumentOutOfRangeException(nameof(MinDistance));
        if (Volume < 0f)
            throw new ArgumentOutOfRangeException(nameof(Volume));

        var snapshot = (SnakeMediaOptions)MemberwiseClone();
        snapshot.Image = (Image ?? throw new ArgumentNullException(nameof(Image))).Snapshot();
        return snapshot;
    }
}

public sealed class SnakeMediaClip
{
    internal SnakeMediaClip(
        IReadOnlyList<VideoFrameData> frames,
        float[] audioSamples)
    {
        Frames = frames;
        AudioSamples = audioSamples;
        DurationSeconds = audioSamples.Length * VoiceChatSettings.SampleToDuartionRate;
    }

    public int FrameCount => Frames.Count;
    public float DurationSeconds { get; }
    internal IReadOnlyList<VideoFrameData> Frames { get; }
    internal float[] AudioSamples { get; }
}

/// <summary>
/// Loading or active synchronized keycard media. Disposing the handle stops
/// both the Snake display and its speaker.
/// </summary>
public sealed class SnakeMediaPlayback : IDisposable
{
    private readonly int _playerId;
    private readonly Task<SnakeMediaClip> _clipTask;
    private readonly SnakeMediaOptions _options;
    private CoroutineHandle _coroutine;
    private SnakeImagePlayback? _imagePlayback;
    private SpeakerApi.Playback _audioPlayback;
    private bool _stopped;

    internal SnakeMediaPlayback(
        int playerId,
        ushort serial,
        Task<SnakeMediaClip> clipTask,
        SnakeMediaOptions options)
    {
        _playerId = playerId;
        Serial = serial;
        _clipTask = clipTask;
        _options = options;
    }

    public ushort Serial { get; }
    public bool IsLoading { get; private set; } = true;
    public bool IsPlaying => !_stopped && _imagePlayback?.IsPlaying == true && _audioPlayback.IsValid;
    public Exception? Error { get; private set; }

    internal void Start()
        => _coroutine = Timing.RunCoroutine(Run());

    public void Stop()
        => Stop(restoreSnake: true, killCoroutine: true);

    public void Dispose()
        => Stop();

    internal void Stop(bool restoreSnake, bool killCoroutine)
    {
        if (_stopped)
            return;

        _stopped = true;
        IsLoading = false;
        if (killCoroutine && _coroutine.IsRunning)
            Timing.KillCoroutines(_coroutine);

        _imagePlayback?.Stop(restoreSnake, killCoroutine: true);
        if (_audioPlayback.ControllerId != 0)
            _audioPlayback.Stop();

        SnakeMediaApi.Remove(this);
    }

    private IEnumerator<float> Run()
    {
        yield return Timing.WaitForOneFrame;

        while (!_clipTask.IsCompleted)
        {
            if (_stopped || !CanContinueLoading())
            {
                Stop(restoreSnake: false, killCoroutine: false);
                yield break;
            }

            yield return Timing.WaitForOneFrame;
        }

        if (_clipTask.IsCanceled)
        {
            Fail(new TaskCanceledException("Snake media loading was canceled."));
            yield break;
        }

        if (_clipTask.IsFaulted)
        {
            Fail(_clipTask.Exception?.GetBaseException() ??
                 new InvalidOperationException("Unknown Snake media loading error."));
            yield break;
        }

        Player? player = Player.Get(_playerId);
        if (!CanStart(player))
        {
            Stop(restoreSnake: false, killCoroutine: false);
            yield break;
        }

        try
        {
            SnakeMediaClip clip = _clipTask.Result;
            _options.Image.TimelineDurationSeconds = clip.DurationSeconds;
            string audioPlayerName = _options.AudioPlayerName ?? $"SnakeMedia_{Serial}";
            SpeakerApi.TryDestroy(audioPlayerName);
            _audioPlayback = SpeakerApi.PlaySamples(
                audioPlayerName,
                clip.AudioSamples,
                player!.Position,
                _options.FollowOwner ? player.Transform : null,
                _options.IsSpatial,
                _options.MaxDistance,
                _options.MinDistance,
                _options.Volume,
                _options.Image.Loop,
                destroyOnEnd: !_options.Image.Loop,
                listeners: _options.Listeners);
            if (!_audioPlayback.IsValid)
                throw new InvalidOperationException("SpeakerApi failed to create media playback.");

            try
            {
                _imagePlayback = SnakeImageApi.PlayFrames(Serial, clip.Frames, _options.Image);
            }
            catch
            {
                _audioPlayback.Stop();
                throw;
            }

            IsLoading = false;
        }
        catch (Exception ex)
        {
            Fail(ex);
            yield break;
        }

        while (!_stopped && _imagePlayback.IsPlaying && _audioPlayback.IsValid)
            yield return Timing.WaitForOneFrame;

        Stop(restoreSnake: true, killCoroutine: false);
    }

    private bool CanContinueLoading()
    {
        Player? player = Player.Get(_playerId);
        return !_options.Image.StopWhenUnequipped || CanStart(player);
    }

    private bool CanStart(Player? player)
        => player != null &&
           player.IsConnected &&
           player.CurrentItem?.Serial == Serial &&
           player.CurrentItem.Base is ChaosKeycardItem;

    private void Fail(Exception error)
    {
        Error = error;
        Log.Error($"[SnakeMediaApi] Playback failed for serial {Serial}: {error}");
        Stop(restoreSnake: false, killCoroutine: false);
    }
}

/// <summary>
/// Downloads, caches, decodes, synchronizes, and stops Chaos Keycard video with
/// one API call.
/// </summary>
public static class SnakeMediaApi
{
    private static readonly object CacheLock = new();
    private static readonly Dictionary<string, Task<SnakeMediaClip>> ClipCache =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<ushort, SnakeMediaPlayback> ActivePlaybacks = new();

    public static IReadOnlyCollection<SnakeMediaPlayback> Active =>
        ActivePlaybacks.Values.ToArray();

    /// <summary>
    /// Convenience entry point for the common pixel-video use case. It enables
    /// owner-session takeover, square pixels, synchronized spatial audio, media
    /// caching, and automatic stop-on-unequip behavior.
    /// </summary>
    public static SnakeMediaPlayback PlayPixelMedia(
        Player player,
        string source,
        float framesPerSecond = 15f,
        int maxFrames = 10000,
        bool invert = false,
        SnakeImageRenderStyle renderStyle = SnakeImageRenderStyle.SolidPixels,
        int abstractionLevel = 2,
        bool loop = true,
        float maxDistance = 12f,
        float minDistance = 1f,
        float volume = 1f,
        string? audioPlayerName = null)
    {
        if (player?.CurrentItem is not Keycard keycard ||
            keycard.Base is not ChaosKeycardItem)
        {
            throw new InvalidOperationException("The player is not holding a Chaos Keycard.");
        }

        return PlayPixelMedia(
            player,
            keycard.Serial,
            source,
            framesPerSecond,
            maxFrames,
            invert,
            renderStyle,
            abstractionLevel,
            loop,
            maxDistance,
            minDistance,
            volume,
            audioPlayerName);
    }

    /// <summary>
    /// Serial overload intended for pre-equip item events such as CItem's
    /// ChangingItem handler.
    /// </summary>
    public static SnakeMediaPlayback PlayPixelMedia(
        Player player,
        ushort keycardSerial,
        string source,
        float framesPerSecond = 15f,
        int maxFrames = 10000,
        bool invert = false,
        SnakeImageRenderStyle renderStyle = SnakeImageRenderStyle.SolidPixels,
        int abstractionLevel = 2,
        bool loop = true,
        float maxDistance = 12f,
        float minDistance = 1f,
        float volume = 1f,
        string? audioPlayerName = null)
        => Play(
            player,
            keycardSerial,
            source,
            new SnakeMediaOptions
            {
                Image = new SnakeImageOptions
                {
                    FramesPerSecond = framesPerSecond,
                    MaxFrames = maxFrames,
                    Invert = invert,
                    RenderStyle = renderStyle,
                    AbstractionLevel = abstractionLevel,
                    Loop = loop,
                    StopWhenUnequipped = true,
                    StopOnSnakeInput = false,
                    RestoreSnakeOnStop = true,
                    TakeOverOwnerSession = true,
                },
                IsSpatial = true,
                FollowOwner = true,
                MaxDistance = maxDistance,
                MinDistance = minDistance,
                Volume = volume,
                AudioPlayerName = audioPlayerName,
            });

    public static SnakeMediaPlayback Play(
        Player player,
        string source,
        SnakeMediaOptions? options = null)
    {
        if (player == null)
            throw new ArgumentNullException(nameof(player));
        if (player.CurrentItem is not Keycard keycard ||
            keycard.Base is not ChaosKeycardItem)
        {
            throw new InvalidOperationException("The player is not holding a Chaos Keycard.");
        }

        return Play(player, keycard.Serial, source, options);
    }

    public static SnakeMediaPlayback Play(
        Player player,
        ushort keycardSerial,
        string source,
        SnakeMediaOptions? options = null)
    {
        if (player == null)
            throw new ArgumentNullException(nameof(player));
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Media source cannot be empty.", nameof(source));

        Item? item = Item.Get(keycardSerial);
        if (item is not Keycard keycard ||
            keycard.Base is not ChaosKeycardItem ||
            keycard.Owner?.Id != player.Id)
        {
            throw new InvalidOperationException(
                "The serial does not identify a Chaos Keycard owned by the player.");
        }

        SnakeMediaOptions resolvedOptions = (options ?? new SnakeMediaOptions()).Snapshot();
        Task<SnakeMediaClip> clipTask = GetOrLoadClip(source, resolvedOptions.Image);
        Stop(keycardSerial);

        var playback = new SnakeMediaPlayback(
            player.Id,
            keycardSerial,
            clipTask,
            resolvedOptions);
        ActivePlaybacks.Add(keycardSerial, playback);
        playback.Start();
        return playback;
    }

    public static bool Stop(ushort serial)
    {
        if (!ActivePlaybacks.TryGetValue(serial, out var playback))
            return false;

        playback.Stop();
        return true;
    }

    public static void StopAll(bool restoreSnake = true)
    {
        foreach (SnakeMediaPlayback playback in ActivePlaybacks.Values.ToArray())
            playback.Stop(restoreSnake, killCoroutine: true);
    }

    internal static void Remove(SnakeMediaPlayback playback)
    {
        if (ActivePlaybacks.TryGetValue(playback.Serial, out var current) &&
            ReferenceEquals(current, playback))
        {
            ActivePlaybacks.Remove(playback.Serial);
        }
    }

    private static Task<SnakeMediaClip> GetOrLoadClip(
        string source,
        SnakeImageOptions image)
    {
        string key = BuildCacheKey(source, image);
        lock (CacheLock)
        {
            if (ClipCache.TryGetValue(key, out Task<SnakeMediaClip>? existing) &&
                !existing.IsCanceled &&
                !existing.IsFaulted)
            {
                return existing;
            }

            Task<SnakeMediaClip> loadTask = Task.Run(() => LoadClip(source, image));
            ClipCache[key] = loadTask;
            return loadTask;
        }
    }

    private static SnakeMediaClip LoadClip(
        string source,
        SnakeImageOptions image)
    {
        bool downloaded = YtDlpApi.IsSupportedUrl(source);
        string mediaPath = downloaded
            ? YtDlpApi.DownloadAudioVideo(source)
            : Path.GetFullPath(source);
        try
        {
            if (!File.Exists(mediaPath))
                throw new FileNotFoundException("Snake media file was not found.", mediaPath);

            Task<IReadOnlyList<VideoFrameData>> framesTask =
                Task.Run<IReadOnlyList<VideoFrameData>>(() =>
                    MediaProcessingApi.GetFramesFromFile(
                        mediaPath,
                        image.Width,
                        image.Height,
                        image.FramesPerSecond,
                        image.MaxFrames,
                        VideoPixelFormat.BlackWhite8,
                        image.Threshold,
                        image.SourceCrop));
            Task<float[]> audioTask =
                Task.Run(() => FfmpegAudioDecoder.DecodeToMono48k(mediaPath));
            Task.WaitAll(framesTask, audioTask);
            return new SnakeMediaClip(framesTask.Result, audioTask.Result);
        }
        finally
        {
            if (downloaded)
                TryDelete(mediaPath);
        }
    }

    private static string BuildCacheKey(
        string source,
        SnakeImageOptions image)
        => string.Join(
            "|",
            source,
            image.Width,
            image.Height,
             image.FramesPerSecond.ToString("R", CultureInfo.InvariantCulture),
             image.MaxFrames,
             image.Threshold,
             image.SourceCrop?.X.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty,
             image.SourceCrop?.Y.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty,
             image.SourceCrop?.Width.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty,
             image.SourceCrop?.Height.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty);

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            Log.Warn($"[SnakeMediaApi] Failed to delete temporary media '{path}': {ex.Message}");
        }
    }
}
