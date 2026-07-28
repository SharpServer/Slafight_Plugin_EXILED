using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Exiled.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Features;

public enum VideoPixelFormat
{
    BlackWhite8,
    Grayscale8,
    Rgb24,
}

/// <summary>
/// A normalized source rectangle applied before media is resized. Coordinates
/// are expressed from the source's top-left corner in the 0-1 range.
/// </summary>
public sealed class VideoSourceCrop
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; } = 1f;
    public float Height { get; set; } = 1f;

    internal VideoSourceCrop Snapshot()
    {
        const float boundaryTolerance = 0.00001f;
        float right = X + Width;
        float bottom = Y + Height;
        if (!IsFinite(X) || !IsFinite(Y) || !IsFinite(Width) || !IsFinite(Height) ||
            X < 0f || X >= 1f || Y < 0f || Y >= 1f ||
            Width <= 0f || Height <= 0f ||
            right > 1f + boundaryTolerance ||
            bottom > 1f + boundaryTolerance)
        {
            throw new ArgumentOutOfRangeException(
                nameof(VideoSourceCrop),
                "Source crop must be a finite normalized rectangle contained within the source image.");
        }

        var snapshot = (VideoSourceCrop)MemberwiseClone();
        snapshot.Width = Math.Min(snapshot.Width, 1f - snapshot.X);
        snapshot.Height = Math.Min(snapshot.Height, 1f - snapshot.Y);
        return snapshot;
    }

    private static bool IsFinite(float value)
        => !float.IsNaN(value) && !float.IsInfinity(value);
}

public sealed class VideoFrameData
{
    public VideoFrameData(
        int index,
        TimeSpan timestamp,
        int width,
        int height,
        VideoPixelFormat pixelFormat,
        byte[] pixels)
    {
        Index = index;
        Timestamp = timestamp;
        Width = width;
        Height = height;
        PixelFormat = pixelFormat;
        Pixels = pixels ?? throw new ArgumentNullException(nameof(pixels));
    }

    public int Index { get; }
    public TimeSpan Timestamp { get; }
    public int Width { get; }
    public int Height { get; }
    public VideoPixelFormat PixelFormat { get; }

    /// <summary>
    /// BlackWhite8 contains 0 or 255 per pixel, Grayscale8 contains 0-255 per
    /// pixel, and Rgb24 contains interleaved R/G/B bytes.
    /// </summary>
    public byte[] Pixels { get; }

    public byte GetGrayscale(int x, int y)
    {
        ValidateCoordinates(x, y);
        var offset = y * Width + x;
        if (PixelFormat != VideoPixelFormat.Rgb24)
            return Pixels[offset];

        offset *= 3;
        return (byte)Math.Min(255,
            (Pixels[offset] * 299 + Pixels[offset + 1] * 587 + Pixels[offset + 2] * 114) / 1000);
    }

    public bool IsWhite(int x, int y, byte threshold = 128)
        => GetGrayscale(x, y) >= threshold;

    private void ValidateCoordinates(int x, int y)
    {
        if (x < 0 || x >= Width)
            throw new ArgumentOutOfRangeException(nameof(x));
        if (y < 0 || y >= Height)
            throw new ArgumentOutOfRangeException(nameof(y));
    }
}

/// <summary>
/// High-level media operations built on yt-dlp and ffmpeg.
/// </summary>
public static class MediaProcessingApi
{
    private const long MaximumFrameBufferBytes = 512L * 1024L * 1024L;

    public static float[] GetAudioSamplesFromUrl(string url, bool keepDownloadedFile = false)
    {
        var path = YtDlpApi.DownloadAudio(url);
        try
        {
            return FfmpegAudioDecoder.DecodeToMono48k(path);
        }
        finally
        {
            if (!keepDownloadedFile)
                TryDelete(path);
        }
    }

    public static SpeakerApi.Playback PlayAudioFromUrl(
        string url,
        string audioPlayerName,
        Vector3 position,
        Transform? parent = null,
        bool isSpatial = false,
        float maxDistance = 5f,
        float minDistance = 1f,
        float volume = 1f,
        bool loop = false,
        bool destroyOnEnd = true,
        Predicate<Player>? listeners = null)
    {
        var samples = GetAudioSamplesFromUrl(url);
        return SpeakerApi.PlaySamples(
            audioPlayerName,
            samples,
            position,
            parent,
            isSpatial,
            maxDistance,
            minDistance,
            volume,
            loop,
            destroyOnEnd: destroyOnEnd && !loop,
            listeners: listeners);
    }

    public static IReadOnlyList<VideoFrameData> GetFramesFromUrl(
        string url,
        int width,
        int height,
        float framesPerSecond = 10f,
        int maxFrames = 300,
        VideoPixelFormat pixelFormat = VideoPixelFormat.Grayscale8,
        byte blackWhiteThreshold = 128,
        bool keepDownloadedFile = false,
        VideoSourceCrop? sourceCrop = null)
    {
        var path = YtDlpApi.DownloadVideo(url);
        try
        {
            return GetFramesFromFile(
                path,
                width,
                height,
                framesPerSecond,
                maxFrames,
                pixelFormat,
                blackWhiteThreshold,
                sourceCrop);
        }
        finally
        {
            if (!keepDownloadedFile)
                TryDelete(path);
        }
    }

    public static IReadOnlyList<VideoFrameData> GetFramesFromFile(
        string fullPath,
        int width,
        int height,
        float framesPerSecond = 10f,
        int maxFrames = 300,
        VideoPixelFormat pixelFormat = VideoPixelFormat.Grayscale8,
        byte blackWhiteThreshold = 128,
        VideoSourceCrop? sourceCrop = null)
    {
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Video file not found: {fullPath}", fullPath);

        return DecodeFrames(
            fullPath,
            $"media file '{fullPath}'",
            width,
            height,
            framesPerSecond,
            maxFrames,
            pixelFormat,
            blackWhiteThreshold,
            sourceCrop);
    }

    /// <summary>
    /// Decodes a direct HTTP(S) image, GIF, or video URL with ffmpeg. Use
    /// <see cref="GetFramesFromUrl"/> instead for webpage URLs that require
    /// yt-dlp extraction.
    /// </summary>
    public static IReadOnlyList<VideoFrameData> GetFramesFromDirectUrl(
        string url,
        int width,
        int height,
        float framesPerSecond = 10f,
        int maxFrames = 300,
        VideoPixelFormat pixelFormat = VideoPixelFormat.Grayscale8,
        byte blackWhiteThreshold = 128,
        VideoSourceCrop? sourceCrop = null)
    {
        if (!YtDlpApi.IsSupportedUrl(url))
            throw new ArgumentException("Only absolute HTTP or HTTPS media URLs are supported.", nameof(url));

        return DecodeFrames(
            url,
            $"media URL '{url}'",
            width,
            height,
            framesPerSecond,
            maxFrames,
            pixelFormat,
            blackWhiteThreshold,
            sourceCrop);
    }

    private static IReadOnlyList<VideoFrameData> DecodeFrames(
        string input,
        string inputDescription,
        int width,
        int height,
        float framesPerSecond,
        int maxFrames,
        VideoPixelFormat pixelFormat,
        byte blackWhiteThreshold,
        VideoSourceCrop? sourceCrop)
    {
        if (width < 1 || width > 4096)
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be between 1 and 4096.");
        if (height < 1 || height > 4096)
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be between 1 and 4096.");
        if (framesPerSecond <= 0f || framesPerSecond > 60f)
            throw new ArgumentOutOfRangeException(nameof(framesPerSecond), "Frame rate must be greater than 0 and at most 60.");
        if (maxFrames < 1 || maxFrames > 10000)
            throw new ArgumentOutOfRangeException(nameof(maxFrames), "maxFrames must be between 1 and 10000.");

        var sourceBytesPerPixel = pixelFormat == VideoPixelFormat.Rgb24 ? 3 : 1;
        var frameSize = checked(width * height * sourceBytesPerPixel);
        if ((long)frameSize * maxFrames > MaximumFrameBufferBytes)
            throw new ArgumentException("Requested video frames can exceed the 512 MiB in-memory safety limit.");

        FfmpegAudioDecoder.EnsureAvailable();
        var ffmpegPixelFormat = pixelFormat == VideoPixelFormat.Rgb24 ? "rgb24" : "gray";
        var fps = framesPerSecond.ToString("0.########", CultureInfo.InvariantCulture);
        VideoSourceCrop? resolvedCrop = sourceCrop?.Snapshot();
        string cropFilter = resolvedCrop == null
            ? string.Empty
            : $"crop=iw*{FormatFilterNumber(resolvedCrop.Width)}:" +
              $"ih*{FormatFilterNumber(resolvedCrop.Height)}:" +
              $"iw*{FormatFilterNumber(resolvedCrop.X)}:" +
              $"ih*{FormatFilterNumber(resolvedCrop.Y)},";
        // A still image has no duration. Without eof_action=pass, ffmpeg's fps
        // filter can legally discard its only frame and exit successfully with
        // an empty output stream.
        var filter = $"{cropFilter}fps={fps}:eof_action=pass," +
                     $"scale={width}:{height}:force_original_aspect_ratio=decrease," +
                     $"pad={width}:{height}:(ow-iw)/2:(oh-ih)/2:color=black";
        var error = new StringBuilder();
        var startInfo = new ProcessStartInfo
        {
            FileName = FfmpegAudioDecoder.ExecutablePath,
            Arguments = $"-v error -nostdin -i \"{EscapeArgument(input)}\" -map 0:v:0 -an " +
                        $"-vf \"{filter}\" -frames:v {maxFrames} -pix_fmt {ffmpegPixelFormat} -f rawvideo pipe:1",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = new Process { StartInfo = startInfo };
        process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
                error.AppendLine(args.Data);
        };

        var frames = new List<VideoFrameData>(Math.Min(maxFrames, 512));
        var started = false;
        try
        {
            if (!process.Start())
                throw new InvalidOperationException("Failed to start ffmpeg video decoder.");

            started = true;
            process.BeginErrorReadLine();
            var stream = process.StandardOutput.BaseStream;
            for (var index = 0; index < maxFrames; index++)
            {
                var pixels = new byte[frameSize];
                var bytesRead = ReadFrame(stream, pixels);
                if (bytesRead == 0)
                    break;
                if (bytesRead != frameSize)
                    throw new InvalidDataException($"ffmpeg returned an incomplete video frame ({bytesRead}/{frameSize} bytes).");

                if (pixelFormat == VideoPixelFormat.BlackWhite8)
                {
                    for (var pixel = 0; pixel < pixels.Length; pixel++)
                        pixels[pixel] = pixels[pixel] >= blackWhiteThreshold ? byte.MaxValue : byte.MinValue;
                }

                frames.Add(new VideoFrameData(
                    index,
                    TimeSpan.FromSeconds(index / (double)framesPerSecond),
                    width,
                    height,
                    pixelFormat,
                    pixels));
            }

            process.WaitForExit();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"ffmpeg failed to decode {inputDescription} (exit code {process.ExitCode}): {error.ToString().Trim()}");
            if (frames.Count == 0)
                throw new InvalidOperationException($"ffmpeg produced no video frames for {inputDescription}.");

            return frames;
        }
        catch
        {
            if (started && !process.HasExited)
            {
                try { process.Kill(); }
                catch { /* ignored */ }
            }

            throw;
        }
    }

    private static int ReadFrame(Stream stream, byte[] buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = stream.Read(buffer, offset, buffer.Length - offset);
            if (read <= 0)
                break;

            offset += read;
        }

        return offset;
    }

    private static string EscapeArgument(string value)
        => value.Replace("\"", "\\\"");

    private static string FormatFilterNumber(float value)
        => value.ToString("0.########", CultureInfo.InvariantCulture);

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            Log.Warn($"[MediaProcessingApi] Failed to delete temporary media '{path}': {ex.Message}");
        }
    }
}
