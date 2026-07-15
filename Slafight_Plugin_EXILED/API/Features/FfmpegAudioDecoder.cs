using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Exiled.API.Features;
using VoiceChat;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// Resolves ffmpeg from EXILED's dependencies directory and decodes audio to the
/// mono 48 kHz float PCM format expected by the in-game voice playback APIs.
/// </summary>
public static class FfmpegAudioDecoder
{
    private const string WindowsDownloadUrl =
        "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
    private const string WindowsChecksumUrl = WindowsDownloadUrl + ".sha256";
    private const string LinuxReleaseBaseUrl =
        "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/";
    private const string LinuxAssetName = "ffmpeg-master-latest-linux64-gpl.tar.xz";
    private const string LinuxChecksumFileName = "checksums.sha256";

    private static readonly object InstallLock = new();
    private static bool IsValidated;

    public static string ExecutablePath =>
        Path.Combine(Paths.Dependencies, IsWindows ? "ffmpeg.exe" : "ffmpeg");

    private static bool IsWindows =>
        Environment.OSVersion.Platform == PlatformID.Win32NT ||
        Environment.OSVersion.Platform == PlatformID.Win32Windows;

    /// <summary>
    /// Ensures ffmpeg is available in EXILED/Plugins/dependencies.
    /// </summary>
    public static void Initialize()
    {
        EnsureAvailable();
        Log.Info($"[FfmpegAudioDecoder] Using ffmpeg: {ExecutablePath}");
    }

    public static void EnsureAvailable()
        => EnsureInstalled();

    public static float[] DecodeToMono48k(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            throw new ArgumentException("Audio file path cannot be empty.", nameof(fullPath));

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Audio file not found: {fullPath}", fullPath);

        EnsureAvailable();

        var error = new StringBuilder();
        var startInfo = new ProcessStartInfo
        {
            FileName = ExecutablePath,
            Arguments = $"-v error -nostdin -i \"{EscapeArgument(fullPath)}\" -map 0:a:0 -vn -ac 1 -ar {VoiceChatSettings.SampleRate} -f f32le pipe:1",
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

        var started = false;
        try
        {
            if (!process.Start())
                throw new InvalidOperationException("Failed to start ffmpeg.");

            started = true;
            process.BeginErrorReadLine();
            using var output = new MemoryStream();
            process.StandardOutput.BaseStream.CopyTo(output);
            process.WaitForExit();
            process.WaitForExit(); // Flush asynchronous stderr events on .NET Framework.

            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"ffmpeg failed to decode '{fullPath}' (exit code {process.ExitCode}): {error.ToString().Trim()}");

            if (output.Length == 0)
                throw new InvalidOperationException($"ffmpeg produced no audio samples for: {fullPath}");

            if (output.Length % sizeof(float) != 0 || output.Length / sizeof(float) > int.MaxValue)
                throw new InvalidOperationException($"ffmpeg produced an invalid PCM stream for: {fullPath}");

            var samples = new float[(int)(output.Length / sizeof(float))];
            Buffer.BlockCopy(output.GetBuffer(), 0, samples, 0, (int)output.Length);
            return samples;
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

    private static void EnsureInstalled()
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

                Log.Warn($"[FfmpegAudioDecoder] Existing ffmpeg is not executable and will be replaced: {ExecutablePath}");
                File.Delete(ExecutablePath);
            }

            var systemExecutable = FindOnPath(IsWindows ? "ffmpeg.exe" : "ffmpeg");
            if (systemExecutable != null)
            {
                File.Copy(systemExecutable, ExecutablePath, overwrite: false);
                EnsureUnixExecutablePermission();
            }
            else if (IsWindows)
                DownloadWindowsBuild();
            else if (Environment.OSVersion.Platform == PlatformID.Unix)
                DownloadLinuxBuild();
            else
                throw new PlatformNotSupportedException(
                    $"Automatic ffmpeg installation is not supported on {Environment.OSVersion.Platform}.");

            if (!TryValidateExecutable(out var validationError))
            {
                TryDelete(ExecutablePath);
                throw new InvalidOperationException($"The installed ffmpeg executable could not be started: {validationError}");
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
                Arguments = "-version",
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
            catch (Exception)
            {
                // Ignore malformed or inaccessible PATH entries.
            }
        }

        return null;
    }

    private static void DownloadWindowsBuild()
    {
        var archivePath = Path.Combine(Paths.Dependencies, $"ffmpeg-{Guid.NewGuid():N}.zip");
        var temporaryExecutable = ExecutablePath + $".{Guid.NewGuid():N}.tmp";

        try
        {
            Log.Info($"[FfmpegAudioDecoder] ffmpeg was not found. Downloading it to {Paths.Dependencies}...");
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2 on older Mono/.NET Framework.

            string expectedChecksum;
            using (var client = new WebClient())
            {
                expectedChecksum = client.DownloadString(WindowsChecksumUrl).Trim();
                client.DownloadFile(WindowsDownloadUrl, archivePath);
            }

            VerifySha256(archivePath, expectedChecksum);

            using (var archiveStream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read))
            {
                var entry = archive.Entries.FirstOrDefault(candidate =>
                    candidate.FullName.Replace('\\', '/').EndsWith("/bin/ffmpeg.exe", StringComparison.OrdinalIgnoreCase));

                if (entry == null)
                    throw new InvalidDataException("The downloaded ffmpeg archive does not contain bin/ffmpeg.exe.");

                using var input = entry.Open();
                using var output = new FileStream(temporaryExecutable, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                input.CopyTo(output);
            }

            File.Move(temporaryExecutable, ExecutablePath);
            Log.Info($"[FfmpegAudioDecoder] Installed ffmpeg at {ExecutablePath}");
        }
        finally
        {
            TryDelete(archivePath);
            TryDelete(temporaryExecutable);
        }
    }

    private static void DownloadLinuxBuild()
    {
        var archivePath = Path.Combine(Paths.Dependencies, $"ffmpeg-{Guid.NewGuid():N}.tar.xz");
        var extractionDirectory = Path.Combine(Paths.Dependencies, $"ffmpeg-extract-{Guid.NewGuid():N}");
        var temporaryExecutable = ExecutablePath + $".{Guid.NewGuid():N}.tmp";

        try
        {
            Log.Info($"[FfmpegAudioDecoder] ffmpeg was not found. Downloading Linux build to {Paths.Dependencies}...");
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;

            string checksums;
            using (var client = new WebClient())
            {
                checksums = client.DownloadString(LinuxReleaseBaseUrl + LinuxChecksumFileName);
                client.DownloadFile(LinuxReleaseBaseUrl + LinuxAssetName, archivePath);
            }

            VerifySha256(archivePath, ParseChecksum(checksums, LinuxAssetName));
            Directory.CreateDirectory(extractionDirectory);
            ExtractTarXz(archivePath, extractionDirectory);

            var extractedExecutable = Directory
                .EnumerateFiles(extractionDirectory, "ffmpeg", SearchOption.AllDirectories)
                .FirstOrDefault();
            if (extractedExecutable == null)
                throw new InvalidDataException("The downloaded Linux ffmpeg archive does not contain an ffmpeg executable.");

            File.Copy(extractedExecutable, temporaryExecutable, overwrite: false);
            File.Move(temporaryExecutable, ExecutablePath);
            EnsureUnixExecutablePermission();
            Log.Info($"[FfmpegAudioDecoder] Installed Linux ffmpeg at {ExecutablePath}");
        }
        finally
        {
            TryDelete(archivePath);
            TryDelete(temporaryExecutable);
            TryDeleteDirectory(extractionDirectory);
        }
    }

    private static void ExtractTarXz(string archivePath, string destinationDirectory)
    {
        var tarExecutable = FindOnPath("tar") ??
                            (File.Exists("/bin/tar") ? "/bin/tar" : null) ??
                            (File.Exists("/usr/bin/tar") ? "/usr/bin/tar" : null);
        if (tarExecutable == null)
            throw new FileNotFoundException("tar is required to extract the downloaded Linux ffmpeg build.");

        var startInfo = new ProcessStartInfo
        {
            FileName = tarExecutable,
            Arguments = $"-xJf \"{EscapeArgument(archivePath)}\" -C \"{EscapeArgument(destinationDirectory)}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = Process.Start(startInfo) ??
                            throw new InvalidOperationException("Failed to start tar for ffmpeg extraction.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        standardOutput.GetAwaiter().GetResult();
        var error = standardError.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Failed to extract Linux ffmpeg build: {error.Trim()}");
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

    private static string EscapeArgument(string value)
        => value.Replace("\"", "\\\"");

    private static void VerifySha256(string filePath, string expectedChecksum)
    {
        if (expectedChecksum.Length != 64 || expectedChecksum.Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidDataException("The ffmpeg download returned an invalid SHA-256 checksum.");

        using var sha256 = SHA256.Create();
        using var input = File.OpenRead(filePath);
        var actualChecksum = BitConverter.ToString(sha256.ComputeHash(input)).Replace("-", string.Empty);
        if (!actualChecksum.Equals(expectedChecksum, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The downloaded ffmpeg archive failed SHA-256 verification.");
    }

    private static string ParseChecksum(string checksums, string assetName)
    {
        foreach (var line in checksums.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[1].TrimStart('*').Equals(assetName, StringComparison.Ordinal))
                return parts[0];
        }

        throw new InvalidDataException($"ffmpeg checksum manifest does not contain {assetName}.");
    }

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

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best effort cleanup only.
        }
    }
}
