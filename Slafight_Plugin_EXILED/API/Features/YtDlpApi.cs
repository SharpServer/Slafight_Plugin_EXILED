using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Exiled.API.Features;

namespace Slafight_Plugin_EXILED.API.Features;

public enum YtDlpMediaKind
{
    Audio,
    Video,
    AudioVideo,
}

/// <summary>
/// Installs and invokes yt-dlp without exposing raw command-line arguments.
/// </summary>
public static class YtDlpApi
{
    private const string ReleaseBaseUrl =
        "https://github.com/yt-dlp/yt-dlp/releases/latest/download/";

    private static readonly object InstallLock = new();
    private static string? ConfiguredCacheDirectory;
    private static bool IsValidated;

    public static string ExecutablePath =>
        Path.Combine(Paths.Dependencies, IsWindows ? "yt-dlp.exe" : "yt-dlp");

    public static string CacheDirectory =>
        ConfiguredCacheDirectory ?? Path.Combine(Paths.Exiled, "ServerContents", ".yt-dlp-cache");

    private static bool IsWindows =>
        Environment.OSVersion.Platform == PlatformID.Win32NT ||
        Environment.OSVersion.Platform == PlatformID.Win32Windows;

    public static void Initialize()
    {
        EnsureAvailable();
        ConfiguredCacheDirectory = Path.Combine(SpeakerApi.AudioDirectory, ".yt-dlp-cache");
        Directory.CreateDirectory(CacheDirectory);
        Log.Info($"[YtDlpApi] Using yt-dlp: {ExecutablePath}");
    }

    public static void EnsureAvailable()
    {
        if (IsValidated && File.Exists(ExecutablePath))
            return;

        lock (InstallLock)
        {
            if (IsValidated && File.Exists(ExecutablePath))
                return;

            Directory.CreateDirectory(Paths.Dependencies);
            if (File.Exists(ExecutablePath))
            {
                EnsureUnixExecutablePermission();
                if (TryValidateExecutable(out _))
                {
                    IsValidated = true;
                    return;
                }

                Log.Warn($"[YtDlpApi] Existing yt-dlp is not executable and will be replaced: {ExecutablePath}");
                File.Delete(ExecutablePath);
            }

            var systemExecutable = FindOnPath(IsWindows ? "yt-dlp.exe" : "yt-dlp");
            if (systemExecutable != null)
            {
                File.Copy(systemExecutable, ExecutablePath, overwrite: false);
                EnsureUnixExecutablePermission();
            }
            else
            {
                DownloadReleaseBinary();
            }

            if (!TryValidateExecutable(out var validationError))
            {
                TryDelete(ExecutablePath);
                throw new InvalidOperationException($"The installed yt-dlp executable could not be started: {validationError}");
            }

            IsValidated = true;
        }
    }

    private static bool TryValidateExecutable(out string error)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = ExecutablePath,
                Arguments = "--no-config --version",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (process == null)
            {
                error = "Process.Start returned null.";
                return false;
            }

            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            standardOutput.GetAwaiter().GetResult();
            error = standardError.GetAwaiter().GetResult().Trim();
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool IsSupportedUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
           (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    public static string DownloadAudio(string url)
        => Download(url, YtDlpMediaKind.Audio);

    public static string DownloadVideo(string url)
        => Download(url, YtDlpMediaKind.Video);

    public static string DownloadAudioVideo(string url)
        => Download(url, YtDlpMediaKind.AudioVideo);

    public static string Download(string url, YtDlpMediaKind mediaKind)
    {
        ValidateUrl(url);
        EnsureAvailable();
        FfmpegAudioDecoder.EnsureAvailable();
        Directory.CreateDirectory(CacheDirectory);

        var outputTemplate = Path.Combine(CacheDirectory, $"media-{Guid.NewGuid():N}.%(ext)s");
        var format = mediaKind switch
        {
            YtDlpMediaKind.Audio => "bestaudio/best",
            YtDlpMediaKind.Video => "bestvideo/best",
            _ => "bestvideo*+bestaudio/best",
        };

        var result = Run(
            "--no-config --no-playlist --no-progress --no-warnings " +
            $"--ffmpeg-location \"{EscapeArgument(FfmpegAudioDecoder.ExecutablePath)}\" " +
            $"-f \"{format}\" -o \"{EscapeArgument(outputTemplate)}\" " +
            $"--print after_move:filepath \"{EscapeArgument(url)}\"");

        var outputPath = result.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .LastOrDefault(File.Exists);

        if (outputPath == null)
            throw new InvalidOperationException("yt-dlp completed without returning a downloaded file path.");

        var fullCachePath = Path.GetFullPath(CacheDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullOutputPath = Path.GetFullPath(outputPath);
        if (!fullOutputPath.StartsWith(fullCachePath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("yt-dlp returned a file outside the media cache directory.");

        return fullOutputPath;
    }

    public static string GetMetadataJson(string url)
    {
        ValidateUrl(url);
        EnsureAvailable();
        return Run(
            "--no-config --no-playlist --no-progress --no-warnings " +
            $"--skip-download --dump-single-json \"{EscapeArgument(url)}\"").StandardOutput.Trim();
    }

    public static IReadOnlyList<string> GetDirectMediaUrls(string url, YtDlpMediaKind mediaKind = YtDlpMediaKind.AudioVideo)
    {
        ValidateUrl(url);
        EnsureAvailable();
        var format = mediaKind switch
        {
            YtDlpMediaKind.Audio => "bestaudio/best",
            YtDlpMediaKind.Video => "bestvideo/best",
            _ => "bestvideo*+bestaudio/best",
        };

        return Run(
                "--no-config --no-playlist --no-progress --no-warnings " +
                $"-f \"{format}\" --get-url \"{EscapeArgument(url)}\"")
            .StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(IsSupportedUrl)
            .ToArray();
    }

    public static int ClearCache(TimeSpan? olderThan = null)
    {
        if (!Directory.Exists(CacheDirectory))
            return 0;

        var cutoff = DateTime.UtcNow - (olderThan ?? TimeSpan.Zero);
        var deleted = 0;
        foreach (var file in Directory.EnumerateFiles(CacheDirectory, "media-*", SearchOption.TopDirectoryOnly))
        {
            if (olderThan.HasValue && File.GetLastWriteTimeUtc(file) > cutoff)
                continue;

            try
            {
                File.Delete(file);
                deleted++;
            }
            catch (Exception ex)
            {
                Log.Warn($"[YtDlpApi] Failed to delete cached media '{file}': {ex.Message}");
            }
        }

        return deleted;
    }

    private static ProcessResult Run(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ExecutablePath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        using var process = Process.Start(startInfo) ??
                            throw new InvalidOperationException("Failed to start yt-dlp.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        var output = standardOutput.GetAwaiter().GetResult();
        var error = standardError.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"yt-dlp failed (exit code {process.ExitCode}): {error.Trim()}");

        return new ProcessResult(output, error);
    }

    private static void DownloadReleaseBinary()
    {
        var assetName = IsWindows ? "yt-dlp.exe" : "yt-dlp_linux";
        var temporaryPath = ExecutablePath + $".{Guid.NewGuid():N}.tmp";

        try
        {
            Log.Info($"[YtDlpApi] yt-dlp was not found. Downloading it to {Paths.Dependencies}...");
            var checksums = MediaToolDownloadApi.DownloadString(ReleaseBaseUrl + "SHA2-256SUMS");
            MediaToolDownloadApi.DownloadFile(ReleaseBaseUrl + assetName, temporaryPath);

            var expectedChecksum = ParseChecksum(checksums, assetName);
            VerifySha256(temporaryPath, expectedChecksum);
            File.Move(temporaryPath, ExecutablePath);
            EnsureUnixExecutablePermission();
            Log.Info($"[YtDlpApi] Installed yt-dlp at {ExecutablePath}");
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private static string ParseChecksum(string checksums, string assetName)
    {
        foreach (var line in checksums.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[1].TrimStart('*').Equals(assetName, StringComparison.Ordinal))
                return parts[0];
        }

        throw new InvalidDataException($"yt-dlp checksum manifest does not contain {assetName}.");
    }

    private static void VerifySha256(string filePath, string expectedChecksum)
    {
        if (expectedChecksum.Length != 64 || expectedChecksum.Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidDataException("yt-dlp returned an invalid SHA-256 checksum.");

        using var sha256 = SHA256.Create();
        using var input = File.OpenRead(filePath);
        var actualChecksum = BitConverter.ToString(sha256.ComputeHash(input)).Replace("-", string.Empty);
        if (!actualChecksum.Equals(expectedChecksum, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The downloaded yt-dlp binary failed SHA-256 verification.");
    }

    private static string? FindOnPath(string executableName)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
            return null;

        foreach (var directory in pathValue.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory))
                continue;

            try
            {
                var candidate = Path.Combine(directory.Trim().Trim('"'), executableName);
                if (File.Exists(candidate))
                    return candidate;
            }
            catch
            {
                // Ignore malformed or inaccessible PATH entries.
            }
        }

        return null;
    }

    private static void EnsureUnixExecutablePermission()
    {
        if (IsWindows)
            return;

        using var chmod = Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/chmod",
            Arguments = $"+x \"{EscapeArgument(ExecutablePath)}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        chmod?.WaitForExit();
    }

    private static void ValidateUrl(string url)
    {
        if (!IsSupportedUrl(url))
            throw new ArgumentException("Only absolute HTTP or HTTPS media URLs are supported.", nameof(url));
    }

    private static string EscapeArgument(string value)
        => value.Replace("\"", "\\\"");

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best effort cleanup only.
        }
    }

    private readonly struct ProcessResult
    {
        public ProcessResult(string standardOutput, string standardError)
        {
            StandardOutput = standardOutput;
            StandardError = standardError;
        }

        public string StandardOutput { get; }
        public string StandardError { get; }
    }
}
