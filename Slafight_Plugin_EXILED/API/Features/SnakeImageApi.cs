using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CentralAuth;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using InventorySystem.Items.Keycards;
using InventorySystem.Items.Keycards.Snake;
using MEC;
using Mirror;
using SNAPI.Events.EventArgs;
using SNAPI.Events.Handlers;
using SNAPI.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Features;

public enum SnakeImageRenderStyle
{
    NativeSnake,
    SolidPixels,
    AbstractSilhouette,
}

public sealed class SnakeImageFrameContext
{
    internal SnakeImageFrameContext(VideoFrameData frame, SnakeImageOptions options)
    {
        Frame = frame;
        Options = options;
    }

    public VideoFrameData Frame { get; }
    public SnakeImageOptions Options { get; }

    public bool IsForeground(int displayX, int displayY)
    {
        if (displayX < 0 || displayX >= Options.Width)
            throw new ArgumentOutOfRangeException(nameof(displayX));
        if (displayY < 0 || displayY >= Options.Height)
            throw new ArgumentOutOfRangeException(nameof(displayY));

        int sourceY = Options.FlipVertically
            ? Options.Height - 1 - displayY
            : displayY;
        bool foreground = Frame.GetGrayscale(displayX, sourceY) >= Options.Threshold;
        return Options.Invert ? !foreground : foreground;
    }

    public Vector2Int ToDisplayPosition(int displayX, int displayY)
        => new(Options.OffsetX + displayX, Options.OffsetY + displayY);
}

/// <summary>
/// Extension point for renderers that mix native head, body, tail, and fallback
/// sprites by controlling segment positions and ordering.
/// </summary>
public interface ISnakeImageFrameRenderer
{
    IReadOnlyList<Vector2Int> Render(SnakeImageFrameContext context);
}

/// <summary>
/// Controls how ffmpeg output is mapped onto the Chaos Keycard's native
/// 18-by-11 Snake display.
/// </summary>
public sealed class SnakeImageOptions
{
    public const int NativeWidth = 18;
    public const int NativeHeight = 11;

    public int Width { get; set; } = NativeWidth;
    public int Height { get; set; } = NativeHeight;
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }
    public float FramesPerSecond { get; set; } = 10f;
    public int MaxFrames { get; set; } = 300;

    /// <summary>
    /// Overrides the time occupied by one pass through all frames. Use the
    /// matching audio duration to keep an animation locked to its audio loop.
    /// </summary>
    public float? TimelineDurationSeconds { get; set; }

    public byte Threshold { get; set; } = 128;
    public bool Invert { get; set; }
    public bool FlipVertically { get; set; } = true;
    public SnakeImageRenderStyle RenderStyle { get; set; } = SnakeImageRenderStyle.NativeSnake;
    public VideoSourceCrop? SourceCrop { get; set; }

    /// <summary>
    /// Controls silhouette simplification from 0 (polarity normalization only)
    /// through 3 (gap filling, diagonal detail connection, and smoothing).
    /// </summary>
    public int AbstractionLevel { get; set; } = 2;

    public ISnakeImageFrameRenderer? CustomRenderer { get; set; }

    /// <summary>
    /// Orders visible cells so the native display chooses its square fallback
    /// sprite instead of directional Snake body sprites.
    /// </summary>
    public bool RenderSolidPixels { get; set; }

    public bool Loop { get; set; } = true;
    public bool RestoreSnakeOnStop { get; set; } = true;
    public bool StopWhenUnequipped { get; set; } = true;
    public bool StopOnSnakeInput { get; set; } = true;

    /// <summary>
    /// Replaces the owning client's locally controlled Snake session with a
    /// server-controlled session before sending the first frame. This is needed
    /// when the owner must see server-authored full-resync messages.
    /// </summary>
    public bool TakeOverOwnerSession { get; set; }

    /// <summary>
    /// Set this for webpage URLs such as YouTube. Direct image, GIF, and media
    /// URLs should leave it disabled so ffmpeg reads the URL directly.
    /// </summary>
    public bool UseYtDlp { get; set; }

    internal SnakeImageOptions Snapshot()
    {
        Validate();
        var snapshot = (SnakeImageOptions)MemberwiseClone();
        snapshot.SourceCrop = SourceCrop?.Snapshot();
        return snapshot;
    }

    private void Validate()
    {
        if (Width < 1 || Width > NativeWidth)
            throw new ArgumentOutOfRangeException(nameof(Width), $"Width must be between 1 and {NativeWidth}.");
        if (Height < 1 || Height > NativeHeight)
            throw new ArgumentOutOfRangeException(nameof(Height), $"Height must be between 1 and {NativeHeight}.");
        if (OffsetX < 0 || OffsetX + Width > NativeWidth)
            throw new ArgumentOutOfRangeException(nameof(OffsetX), "The image must fit within the native display width.");
        if (OffsetY < 0 || OffsetY + Height > NativeHeight)
            throw new ArgumentOutOfRangeException(nameof(OffsetY), "The image must fit within the native display height.");
        if (FramesPerSecond <= 0f || FramesPerSecond > 30f)
            throw new ArgumentOutOfRangeException(nameof(FramesPerSecond), "Frame rate must be greater than 0 and at most 30.");
        if (MaxFrames < 1 || MaxFrames > 10000)
            throw new ArgumentOutOfRangeException(nameof(MaxFrames), "MaxFrames must be between 1 and 10000.");
        if (TimelineDurationSeconds is <= 0f)
            throw new ArgumentOutOfRangeException(
                nameof(TimelineDurationSeconds),
                "Timeline duration must be greater than zero when specified.");
        if (AbstractionLevel < 0 || AbstractionLevel > 3)
            throw new ArgumentOutOfRangeException(
                nameof(AbstractionLevel),
                "Abstraction level must be between 0 and 3.");
    }
}

/// <summary>
/// A running image or animation on a Chaos Keycard. Dispose this handle to stop
/// playback and restore the current SNAPI Snake state.
/// </summary>
public sealed class SnakeImagePlayback : IDisposable
{
    private readonly SnakeContext _context;
    private readonly ChaosKeycardItem _keycard;
    private readonly IReadOnlyList<List<Vector2Int>> _frames;
    private readonly SnakeImageOptions _options;
    private CoroutineHandle _coroutine;
    private bool _ownerSessionTakenOver;
    private bool _stopped;

    internal SnakeImagePlayback(
        SnakeContext context,
        ChaosKeycardItem keycard,
        IReadOnlyList<List<Vector2Int>> frames,
        SnakeImageOptions options)
    {
        _context = context;
        _keycard = keycard;
        _frames = frames;
        _options = options;
        Serial = context.Keycard.Serial;
    }

    public ushort Serial { get; }
    public int FrameCount => _frames.Count;
    public bool IsPlaying => !_stopped;
    internal bool StopsOnSnakeInput => _options.StopOnSnakeInput;
    internal bool BlocksClientSnakeSync => !_stopped && _options.TakeOverOwnerSession;

    internal void Start()
        => _coroutine = Timing.RunCoroutine(PlaybackCoroutine());

    public void Stop()
        => Stop(_options.RestoreSnakeOnStop, killCoroutine: true);

    public void Dispose()
        => Stop();

    internal void Stop(bool restoreSnake, bool killCoroutine)
    {
        if (_stopped)
            return;

        _stopped = true;
        if (killCoroutine)
            Timing.KillCoroutines(_coroutine);

        if (restoreSnake)
            TryRestoreSnake();

        SnakeImageApi.Remove(this);
    }

    private IEnumerator<float> PlaybackCoroutine()
    {
        var frameDelay = 1f / _options.FramesPerSecond;

        if (_frames.Count == 1)
        {
            if (!TrySend(_frames[0]))
            {
                Stop(restoreSnake: false, killCoroutine: false);
                yield break;
            }

            if (_options.Loop)
            {
                while (!_stopped && IsValidTarget())
                {
                    yield return Timing.WaitForSeconds(Math.Min(0.25f, frameDelay));

                    // The inspected card retains its original local SnakeEngine
                    // even after the full-sync takeover. Reassert a static frame
                    // like a one-frame video so a late in-flight delta cannot
                    // leave the owner's display corrupted.
                    if (_options.TakeOverOwnerSession && !TrySend(_frames[0]))
                    {
                        Stop(restoreSnake: false, killCoroutine: false);
                        yield break;
                    }
                }
            }
            else
            {
                yield return Timing.WaitForSeconds(frameDelay);
            }

            Stop(_options.RestoreSnakeOnStop, killCoroutine: false);
            yield break;
        }

        var timelineDuration = _options.TimelineDurationSeconds ??
                               _frames.Count / _options.FramesPerSecond;
        var timeline = Stopwatch.StartNew();
        long lastSequence = -1;

        while (!_stopped)
        {
            if (!IsValidTarget())
            {
                Stop(restoreSnake: false, killCoroutine: false);
                yield break;
            }

            var elapsed = timeline.Elapsed.TotalSeconds;
            if (!_options.Loop && elapsed >= timelineDuration)
            {
                if (lastSequence != _frames.Count - 1 && !TrySend(_frames[_frames.Count - 1]))
                {
                    Stop(restoreSnake: false, killCoroutine: false);
                    yield break;
                }

                break;
            }

            var cycle = _options.Loop
                ? (long)(elapsed / timelineDuration)
                : 0L;
            var cycleTime = _options.Loop
                ? elapsed - cycle * timelineDuration
                : elapsed;
            var frameIndex = Math.Min(
                _frames.Count - 1,
                (int)(cycleTime / timelineDuration * _frames.Count));
            var sequence = cycle * _frames.Count + frameIndex;
            if (sequence != lastSequence)
            {
                if (!TrySend(_frames[frameIndex]))
                {
                    Stop(restoreSnake: false, killCoroutine: false);
                    yield break;
                }

                lastSequence = sequence;
            }

            yield return Timing.WaitForOneFrame;
        }

        Stop(_options.RestoreSnakeOnStop, killCoroutine: false);
    }

    private bool TrySend(List<Vector2Int> frame)
    {
        if (!IsValidTarget())
            return false;

        var message = SnakeNetworkMessage.NewFullResync(
            gameover: false,
            frame,
            nextFood: null);

        if (_options.TakeOverOwnerSession && !_ownerSessionTakenOver)
        {
            if (!TryTakeOverOwnerSession(message))
                return false;

            return true;
        }

        _keycard.ServerSendMessage(message);
        return true;
    }

    private bool TryTakeOverOwnerSession(SnakeNetworkMessage firstFrame)
    {
        ReferenceHub owner = _keycard.Owner;
        if (!IsReadyClient(owner))
        {
            Log.Warn(
                $"[SnakeImageApi] Cannot take over the owner Snake session for serial {Serial}: " +
                "the owning client is not ready.");
            return false;
        }

        try
        {
            var sessions = ChaosKeycardItem.SnakeSessions.ToArray();
            _keycard.ServerSendTargetRpc(owner, writer =>
            {
                writer.WriteByte((byte)KeycardItem.MsgType.Custom);
                writer.WriteByte((byte)ChaosKeycardItem.ChaosMsgType.NewConnectionFullSync);

                var includedTarget = false;
                foreach (var session in sessions)
                {
                    writer.WriteUShort(session.Key);
                    if (session.Key == Serial)
                    {
                        firstFrame.WriteSelf(writer);
                        includedTarget = true;
                    }
                    else
                    {
                        session.Value.WriteFullResyncMessage(writer);
                    }
                }

                if (!includedTarget)
                {
                    writer.WriteUShort(Serial);
                    firstFrame.WriteSelf(writer);
                }
            });

            _ownerSessionTakenOver = true;
            Log.Info(
                $"[SnakeImageApi] Replaced the owner Snake session for serial {Serial} " +
                $"and preserved {sessions.Length} server session(s).");
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn(
                $"[SnakeImageApi] Failed to take over the owner Snake session for serial {Serial}: {ex}");
            return false;
        }
    }

    private static bool IsReadyClient(ReferenceHub? hub)
    {
        try
        {
            return hub != null &&
                   hub.Mode == ClientInstanceMode.ReadyClient &&
                   hub.netId != 0 &&
                   hub.connectionToClient is { isReady: true };
        }
        catch
        {
            return false;
        }
    }

    private bool IsValidTarget()
    {
        Item? current = Item.Get(Serial);
        if (current == null || !ReferenceEquals(current.Base, _keycard))
            return false;

        return !_options.StopWhenUnequipped || _context.Player?.CurrentItem?.Serial == Serial;
    }

    private void TryRestoreSnake()
    {
        try
        {
            Item? current = Item.Get(Serial);
            if (current == null || !ReferenceEquals(current.Base, _keycard))
                return;

            SnakeContext? currentContext = SnakeContext.Get(Serial);
            if (currentContext == null)
                return;

            var segments = currentContext.Segments?.Count >= 2
                ? new List<Vector2Int>(currentContext.Segments)
                : SnakeImageApi.CreateDefaultSnakeSegments();

            _keycard.ServerSendMessage(
                SnakeNetworkMessage.NewFullResync(
                    gameover: false,
                    segments,
                    currentContext.NextFoodPosition));
        }
        catch (Exception ex)
        {
            Log.Warn($"[SnakeImageApi] Failed to restore Snake display for serial {Serial}: {ex.Message}");
        }
    }
}

/// <summary>
/// Converts images, GIFs, and videos with ffmpeg and displays them through
/// SNAPI on the Chaos Keycard's Snake screen.
/// </summary>
public static class SnakeImageApi
{
    private static readonly Dictionary<ushort, SnakeImagePlayback> ActivePlaybacks = new();
    private static bool _eventsRegistered;

    public static IReadOnlyCollection<SnakeImagePlayback> Active =>
        ActivePlaybacks.Values.ToArray();

    internal static void RegisterEvents()
    {
        if (_eventsRegistered)
            return;

        Exiled.Events.Handlers.Server.WaitingForPlayers += OnWaitingForPlayers;
        SnakePlayer.SnakeMove += OnSnakeMove;
        _eventsRegistered = true;
    }

    internal static void UnregisterEvents()
    {
        if (!_eventsRegistered)
            return;

        SnakeMediaApi.StopAll(restoreSnake: false);
        Exiled.Events.Handlers.Server.WaitingForPlayers -= OnWaitingForPlayers;
        SnakePlayer.SnakeMove -= OnSnakeMove;
        _eventsRegistered = false;
        StopAll(restoreSnake: false);
    }

    /// <summary>
    /// Automatically treats an existing path as a local file and an HTTP(S)
    /// location as a direct media URL.
    /// </summary>
    public static SnakeImagePlayback Play(
        Player player,
        string location,
        SnakeImageOptions? options = null)
        => Play(GetHeldChaosKeycard(player).Serial, location, options);

    public static SnakeImagePlayback Play(
        Keycard keycard,
        string location,
        SnakeImageOptions? options = null)
        => Play(keycard?.Serial ?? throw new ArgumentNullException(nameof(keycard)), location, options);

    public static SnakeImagePlayback Play(
        ushort keycardSerial,
        string location,
        SnakeImageOptions? options = null)
    {
        if (File.Exists(location))
            return PlayFile(keycardSerial, location, options);
        if (YtDlpApi.IsSupportedUrl(location))
            return PlayUrl(keycardSerial, location, options);

        throw new FileNotFoundException(
            "The image location is neither an existing file nor an absolute HTTP(S) URL.",
            location);
    }

    public static SnakeImagePlayback PlayFile(
        Keycard keycard,
        string filePath,
        SnakeImageOptions? options = null)
        => PlayFile(keycard?.Serial ?? throw new ArgumentNullException(nameof(keycard)), filePath, options);

    public static SnakeImagePlayback PlayFile(
        ushort keycardSerial,
        string filePath,
        SnakeImageOptions? options = null)
    {
        var resolvedOptions = (options ?? new SnakeImageOptions()).Snapshot();
        var frames = MediaProcessingApi.GetFramesFromFile(
            filePath,
            resolvedOptions.Width,
            resolvedOptions.Height,
            resolvedOptions.FramesPerSecond,
            resolvedOptions.MaxFrames,
            VideoPixelFormat.Grayscale8,
            resolvedOptions.Threshold,
            resolvedOptions.SourceCrop);
        return PlayFrames(keycardSerial, frames, resolvedOptions);
    }

    public static SnakeImagePlayback PlayUrl(
        Keycard keycard,
        string url,
        SnakeImageOptions? options = null)
        => PlayUrl(keycard?.Serial ?? throw new ArgumentNullException(nameof(keycard)), url, options);

    public static SnakeImagePlayback PlayUrl(
        ushort keycardSerial,
        string url,
        SnakeImageOptions? options = null)
    {
        var resolvedOptions = (options ?? new SnakeImageOptions()).Snapshot();
        var frames = resolvedOptions.UseYtDlp
            ? MediaProcessingApi.GetFramesFromUrl(
                url,
                resolvedOptions.Width,
                resolvedOptions.Height,
                resolvedOptions.FramesPerSecond,
                resolvedOptions.MaxFrames,
                VideoPixelFormat.Grayscale8,
                resolvedOptions.Threshold,
                sourceCrop: resolvedOptions.SourceCrop)
            : MediaProcessingApi.GetFramesFromDirectUrl(
                url,
                resolvedOptions.Width,
                resolvedOptions.Height,
                resolvedOptions.FramesPerSecond,
                resolvedOptions.MaxFrames,
                VideoPixelFormat.Grayscale8,
                resolvedOptions.Threshold,
                resolvedOptions.SourceCrop);
        return PlayFrames(keycardSerial, frames, resolvedOptions);
    }

    public static SnakeImagePlayback PlayFrames(
        ushort keycardSerial,
        IReadOnlyList<VideoFrameData> frames,
        SnakeImageOptions? options = null)
    {
        if (frames == null)
            throw new ArgumentNullException(nameof(frames));
        if (frames.Count == 0)
            throw new ArgumentException("At least one frame is required.", nameof(frames));

        var resolvedOptions = (options ?? new SnakeImageOptions()).Snapshot();
        SnakeContext context = SnakeContext.Get(keycardSerial) ??
                               throw new ArgumentException(
                                   "The serial does not identify a Chaos Keycard known to SNAPI.",
                                   nameof(keycardSerial));
        if (context.Keycard.Base is not ChaosKeycardItem keycard)
            throw new ArgumentException("The serial does not identify a Chaos Keycard.", nameof(keycardSerial));

        var renderedFrames = frames
            .Select(frame => CreateSegments(frame, resolvedOptions))
            .ToArray();

        Stop(keycardSerial);
        var playback = new SnakeImagePlayback(context, keycard, renderedFrames, resolvedOptions);
        ActivePlaybacks.Add(keycardSerial, playback);
        playback.Start();
        return playback;
    }

    public static bool Stop(ushort keycardSerial)
    {
        if (!ActivePlaybacks.TryGetValue(keycardSerial, out var playback))
            return false;

        playback.Stop();
        return true;
    }

    public static void StopAll(bool restoreSnake = true)
    {
        foreach (var playback in ActivePlaybacks.Values.ToArray())
            playback.Stop(restoreSnake, killCoroutine: true);
    }

    internal static void Remove(SnakeImagePlayback playback)
    {
        if (ActivePlaybacks.TryGetValue(playback.Serial, out var current) &&
            ReferenceEquals(current, playback))
        {
            ActivePlaybacks.Remove(playback.Serial);
        }
    }

    internal static bool BlocksClientSnakeSync(ushort serial)
        => ActivePlaybacks.TryGetValue(serial, out SnakeImagePlayback? playback) &&
           playback.BlocksClientSnakeSync;

    internal static List<Vector2Int> CreateDefaultSnakeSegments() =>
    [
        new(9, 5),
        new(8, 5),
        new(7, 5),
        new(6, 5),
        new(5, 5),
    ];

    private static List<Vector2Int> CreateSegments(
        VideoFrameData frame,
        SnakeImageOptions options)
    {
        if (frame.Width != options.Width || frame.Height != options.Height)
            throw new ArgumentException(
                $"Frame size {frame.Width}x{frame.Height} does not match the configured " +
                $"{options.Width}x{options.Height} display region.");

        var context = new SnakeImageFrameContext(frame, options);
        if (options.CustomRenderer != null)
        {
            IReadOnlyList<Vector2Int> rendered =
                options.CustomRenderer.Render(context) ??
                throw new InvalidOperationException("The custom Snake frame renderer returned null.");
            return EnsureRenderableSegments(new List<Vector2Int>(rendered));
        }

        var mask = new bool[options.Height, options.Width];
        for (var outputY = 0; outputY < frame.Height; outputY++)
        {
            for (var outputX = 0; outputX < frame.Width; outputX++)
                mask[outputY, outputX] = context.IsForeground(outputX, outputY);
        }

        SnakeImageRenderStyle renderStyle = options.RenderStyle;
        if (options.RenderSolidPixels && renderStyle == SnakeImageRenderStyle.NativeSnake)
            renderStyle = SnakeImageRenderStyle.SolidPixels;
        if (renderStyle == SnakeImageRenderStyle.AbstractSilhouette)
            ApplyAbstraction(mask, options.AbstractionLevel);

        var segments = new List<Vector2Int>(frame.Width * frame.Height);
        for (var outputY = 0; outputY < frame.Height; outputY++)
        {
            bool reverseRow = outputY % 2 != 0;
            for (var column = 0; column < frame.Width; column++)
            {
                int outputX = reverseRow ? frame.Width - 1 - column : column;
                if (mask[outputY, outputX])
                    segments.Add(context.ToDisplayPosition(outputX, outputY));
            }
        }

        if (renderStyle is SnakeImageRenderStyle.SolidPixels or
            SnakeImageRenderStyle.AbstractSilhouette &&
            segments.Count > 0)
        {
            segments = CreateSolidPixelSegments(segments);
        }

        return EnsureRenderableSegments(segments);
    }

    private static void ApplyAbstraction(bool[,] mask, int level)
    {
        int height = mask.GetLength(0);
        int width = mask.GetLength(1);
        int foreground = 0;
        foreach (bool value in mask)
        {
            if (value)
                foreground++;
        }

        if (foreground > width * height / 2)
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                    mask[y, x] = !mask[y, x];
            }
        }

        if (level >= 1)
            FillSingleCellGaps(mask);
        if (level >= 2)
            ConnectDiagonalDetails(mask);
        if (level >= 3)
            SmoothMask(mask);
    }

    private static void FillSingleCellGaps(bool[,] mask)
    {
        int height = mask.GetLength(0);
        int width = mask.GetLength(1);
        var source = (bool[,])mask.Clone();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (source[y, x])
                    continue;

                bool horizontal = x > 0 && x + 1 < width &&
                                  source[y, x - 1] && source[y, x + 1];
                bool vertical = y > 0 && y + 1 < height &&
                                source[y - 1, x] && source[y + 1, x];
                if (horizontal || vertical)
                    mask[y, x] = true;
            }
        }
    }

    private static void ConnectDiagonalDetails(bool[,] mask)
    {
        int height = mask.GetLength(0);
        int width = mask.GetLength(1);
        var source = (bool[,])mask.Clone();
        for (var y = 0; y + 1 < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (!source[y, x])
                    continue;

                if (x + 1 < width &&
                    source[y + 1, x + 1] &&
                    !source[y, x + 1] &&
                    !source[y + 1, x])
                {
                    mask[y + 1, x] = true;
                }

                if (x > 0 &&
                    source[y + 1, x - 1] &&
                    !source[y, x - 1] &&
                    !source[y + 1, x])
                {
                    mask[y + 1, x] = true;
                }
            }
        }
    }

    private static void SmoothMask(bool[,] mask)
    {
        int height = mask.GetLength(0);
        int width = mask.GetLength(1);
        var source = (bool[,])mask.Clone();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                int neighbors = 0;
                for (var offsetY = -1; offsetY <= 1; offsetY++)
                {
                    for (var offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        if (offsetX == 0 && offsetY == 0)
                            continue;

                        int neighborX = x + offsetX;
                        int neighborY = y + offsetY;
                        if (neighborX >= 0 && neighborX < width &&
                            neighborY >= 0 && neighborY < height &&
                            source[neighborY, neighborX])
                        {
                            neighbors++;
                        }
                    }
                }

                mask[y, x] = source[y, x]
                    ? neighbors >= 2
                    : neighbors >= 5;
            }
        }
    }

    private static List<Vector2Int> EnsureRenderableSegments(List<Vector2Int> segments)
    {
        if (segments.Count > byte.MaxValue)
            throw new InvalidOperationException(
                $"Snake frame contains {segments.Count} segments; the network limit is {byte.MaxValue}.");

        if (segments.Count == 0)
        {
            for (var x = -120; x < -115; x++)
                segments.Add(new Vector2Int(x, -120));
        }
        else
        {
            while (segments.Count < 5)
                segments.Add(segments[segments.Count - 1]);
        }

        return segments;
    }

    private static List<Vector2Int> CreateSolidPixelSegments(
        IReadOnlyCollection<Vector2Int> pixels)
    {
        const int hiddenCoordinate = -120;
        const int hiddenMinimumSide = -120;
        const int hiddenMaximumSide = 120;

        var rows = pixels
            .GroupBy(pixel => pixel.y)
            .OrderBy(row => row.Key)
            .Select(row => row.OrderBy(pixel => pixel.x).ToArray())
            .ToArray();
        var result = new List<Vector2Int>(pixels.Count + rows.Length * 2 + 2);
        var startFromMinimum = true;

        var firstHiddenX = startFromMinimum ? hiddenMaximumSide : hiddenMinimumSide;
        result.Add(new Vector2Int(firstHiddenX, hiddenCoordinate));
        result.Add(new Vector2Int(firstHiddenX, rows[0][0].y));

        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            var row = rows[rowIndex];
            AddAlternatingRow(result, row, startFromMinimum);

            var endsAtMinimum = row.Length % 2 == 1
                ? startFromMinimum
                : !startFromMinimum;
            var bridgeX = endsAtMinimum ? hiddenMaximumSide : hiddenMinimumSide;
            result.Add(new Vector2Int(bridgeX, row[0].y));

            if (rowIndex + 1 < rows.Length)
            {
                var nextRowY = rows[rowIndex + 1][0].y;
                result.Add(new Vector2Int(bridgeX, nextRowY));
                startFromMinimum = endsAtMinimum;
            }
            else
            {
                result.Add(new Vector2Int(bridgeX, hiddenCoordinate));
            }
        }

        return result;
    }

    private static void AddAlternatingRow(
        ICollection<Vector2Int> output,
        IReadOnlyList<Vector2Int> row,
        bool startFromMinimum)
    {
        var minimum = 0;
        var maximum = row.Count - 1;
        var takeMinimum = startFromMinimum;
        while (minimum <= maximum)
        {
            output.Add(takeMinimum ? row[minimum++] : row[maximum--]);
            takeMinimum = !takeMinimum;
        }
    }

    private static void OnWaitingForPlayers()
    {
        SnakeMediaApi.StopAll(restoreSnake: false);
        StopAll(restoreSnake: false);
    }

    private static void OnSnakeMove(SnakeMoveEventArgs ev)
    {
        if (ActivePlaybacks.TryGetValue(ev.Keycard.Serial, out var playback) &&
            playback.StopsOnSnakeInput)
        {
            playback.Stop();
        }
    }

    private static Keycard GetHeldChaosKeycard(Player player)
    {
        if (player == null)
            throw new ArgumentNullException(nameof(player));
        if (player.CurrentItem is not Keycard keycard ||
            keycard.Base is not ChaosKeycardItem)
        {
            throw new InvalidOperationException("The player is not holding a Chaos Keycard.");
        }

        return keycard;
    }
}
