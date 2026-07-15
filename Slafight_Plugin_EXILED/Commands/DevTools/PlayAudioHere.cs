using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using CommandSystem;
using Exiled.API.Features;
using MEC;
using Slafight_Plugin_EXILED.API.Features;

namespace Slafight_Plugin_EXILED.Commands.DevTools;

public class PlayAudioHere : ICommand
{
    private static readonly Dictionary<string, CoroutineHandle> PendingUrlPlaybacks =
        new(StringComparer.OrdinalIgnoreCase);

    public string Command => "playhere";
    public string[] Aliases { get; } = ["playaudiohere", "pah"];
    public string Description => "Play local audio or a yt-dlp-supported media URL with loop, range, volume, and follow options.";

    public static int CancelAllPending()
    {
        var count = PendingUrlPlaybacks.Count;
        foreach (var handle in PendingUrlPlaybacks.Values)
            Timing.KillCoroutines(handle);

        PendingUrlPlaybacks.Clear();
        return count;
    }

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!CommandTools.CheckPermission(sender, Command, out response))
            return false;

        if (!CommandTools.TryGetExecutor(sender, out var executor, out response))
            return false;

        if (arguments.Count == 0)
        {
            response = Usage;
            return false;
        }

        if (arguments.At(0).Equals("--stop", StringComparison.OrdinalIgnoreCase))
            return Stop(arguments, out response);

        var source = arguments.At(0);
        var isUrl = YtDlpApi.IsSupportedUrl(source);
        if (!isUrl && !TryGetAudioFile(source, out response))
            return false;

        var loop = false;
        var maxDistance = 15f;
        var minDistance = 1f;
        var volume = 1f;
        var player = executor;
        var followPlayer = false;
        string audioPlayerName = null;
        var legacyDistanceIndex = 0;

        for (var index = 1; index < arguments.Count; index++)
        {
            var option = arguments.At(index);
            switch (option.ToLowerInvariant())
            {
                case "--loop":
                case "-l":
                    loop = true;
                    break;

                case "--player":
                case "--follow":
                case "-p":
                    if (!TryGetOptionValue(arguments, ref index, option, out var playerValue, out response) ||
                        !CommandTools.TryResolvePlayer(playerValue, executor, out player, out response))
                        return false;

                    followPlayer = true;
                    break;

                case "--max":
                    if (!TryGetFloatOption(arguments, ref index, option, 0f, out maxDistance, out response))
                        return false;
                    break;

                case "--min":
                    if (!TryGetFloatOption(arguments, ref index, option, 0f, out minDistance, out response))
                        return false;
                    break;

                case "--volume":
                case "-v":
                    if (!TryGetFloatOption(arguments, ref index, option, 0f, out volume, out response))
                        return false;
                    break;

                case "--name":
                case "-n":
                    if (!TryGetOptionValue(arguments, ref index, option, out audioPlayerName, out response))
                        return false;
                    break;

                default:
                    // Preserve the original: <file> [maxDistance] [minDistance].
                    if (!TryParseDistance(option, out var legacyDistance) || legacyDistanceIndex >= 2)
                    {
                        response = $"Unknown option or invalid distance: {option}\n{Usage}";
                        return false;
                    }

                    if (legacyDistanceIndex++ == 0)
                        maxDistance = legacyDistance;
                    else
                        minDistance = legacyDistance;
                    break;
            }
        }

        minDistance = Math.Max(1f, minDistance);
        if (minDistance > maxDistance)
        {
            response = "minDistance cannot be greater than maxDistance.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(audioPlayerName))
            audioPlayerName = $"PlayHere_{executor.Id}_{DateTime.UtcNow.Ticks}";

        if (isUrl)
        {
            if (PendingUrlPlaybacks.ContainsKey(audioPlayerName))
            {
                response = $"A URL download named '{audioPlayerName}' is already pending.";
                return false;
            }

            var initialPosition = player.Position;
            var handle = Timing.RunCoroutine(DownloadAndPlayUrl(
                source,
                audioPlayerName,
                player,
                initialPosition,
                followPlayer,
                loop,
                maxDistance,
                minDistance,
                volume,
                sender));
            PendingUrlPlaybacks[audioPlayerName] = handle;
            response = $"Downloading media audio with yt-dlp. Playback name: {audioPlayerName}";
            return true;
        }

        try
        {
            if (loop)
            {
                SpeakerApi.PlayLoop(source, audioPlayerName, player.Position,
                    parent: followPlayer ? player.Transform : null,
                    isSpatial: true, maxDistance: maxDistance, minDistance: minDistance, volume: volume);
            }
            else
            {
                SpeakerApi.Play(source, audioPlayerName, player.Position, destroyOnEnd: true,
                    parent: followPlayer ? player.Transform : null,
                    isSpatial: true, maxDistance: maxDistance, minDistance: minDistance, volume: volume);
            }

            var location = followPlayer ? $"following {player.Nickname}" : $"at {player.Nickname}'s current position";
            response = $"Playing {source} {location}. Name: {audioPlayerName}; " +
                       $"{(loop ? "looping" : "one-shot")}; range: {minDistance:0.##}-{maxDistance:0.##}; volume: {volume:0.##}.";
            return true;
        }
        catch (Exception ex)
        {
            response = $"Failed to play {source}: {ex.Message}";
            return false;
        }
    }

    private static IEnumerator<float> DownloadAndPlayUrl(
        string url,
        string audioPlayerName,
        Player player,
        UnityEngine.Vector3 initialPosition,
        bool followPlayer,
        bool loop,
        float maxDistance,
        float minDistance,
        float volume,
        ICommandSender sender)
    {
        var downloadTask = Task.Run(() => MediaProcessingApi.GetAudioSamplesFromUrl(url));
        while (!downloadTask.IsCompleted)
            yield return Timing.WaitForOneFrame;

        PendingUrlPlaybacks.Remove(audioPlayerName);
        if (downloadTask.IsFaulted)
        {
            var error = downloadTask.Exception?.GetBaseException().Message ?? "Unknown yt-dlp error.";
            sender.Respond($"Failed to download/play media URL: {error}", false);
            yield break;
        }

        if (downloadTask.IsCanceled)
        {
            sender.Respond($"Media URL playback was canceled: {audioPlayerName}", false);
            yield break;
        }

        if (followPlayer && (player == null || !player.IsConnected))
        {
            sender.Respond("The followed player disconnected before the media download completed.", false);
            yield break;
        }

        try
        {
            if (loop)
                SpeakerApi.TryDestroy(audioPlayerName);

            var position = followPlayer ? player.Position : initialPosition;
            SpeakerApi.PlaySamples(
                audioPlayerName,
                downloadTask.Result,
                position,
                parent: followPlayer ? player.Transform : null,
                isSpatial: true,
                maxDistance: maxDistance,
                minDistance: minDistance,
                volume: volume,
                loop: loop,
                destroyOnEnd: !loop);
            sender.Respond($"Playing downloaded media audio. Name: {audioPlayerName}", true);
        }
        catch (Exception ex)
        {
            sender.Respond($"Failed to start downloaded media audio: {ex.Message}", false);
        }
    }

    private static bool Stop(ArraySegment<string> arguments, out string response)
    {
        if (arguments.Count != 2 || string.IsNullOrWhiteSpace(arguments.At(1)))
        {
            response = "Usage: sl playhere --stop <name>";
            return false;
        }

        var audioPlayerName = arguments.At(1);
        if (PendingUrlPlaybacks.TryGetValue(audioPlayerName, out var pending))
        {
            Timing.KillCoroutines(pending);
            PendingUrlPlaybacks.Remove(audioPlayerName);
            response = $"Canceled pending media URL playback: {audioPlayerName}";
            return true;
        }

        if (!SpeakerApi.TryDestroy(audioPlayerName))
        {
            response = $"No active audio player named: {audioPlayerName}";
            return false;
        }

        response = $"Stopped audio player: {audioPlayerName}";
        return true;
    }

    private static bool TryGetAudioFile(string fileName, out string response)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            response = "Audio file name cannot be empty.";
            return false;
        }

        if (Path.IsPathRooted(fileName) || fileName.Contains(".."))
        {
            response = "Specify a file relative to AudioReferences.";
            return false;
        }

        var audioDirectory = SpeakerApi.AudioDirectory;
        var fullPath = Path.GetFullPath(Path.Combine(audioDirectory, fileName));
        var audioRoot = Path.GetFullPath(audioDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(audioRoot, StringComparison.OrdinalIgnoreCase))
        {
            response = "Specify a file inside AudioReferences.";
            return false;
        }

        if (!File.Exists(fullPath))
        {
            response = $"Audio file not found: {fullPath}";
            return false;
        }

        response = string.Empty;
        return true;
    }

    private static bool TryGetOptionValue(ArraySegment<string> arguments, ref int index, string option, out string value, out string response)
    {
        if (++index < arguments.Count && !string.IsNullOrWhiteSpace(arguments.At(index)))
        {
            value = arguments.At(index);
            response = string.Empty;
            return true;
        }

        value = string.Empty;
        response = $"Missing value for {option}.";
        return false;
    }

    private static bool TryGetFloatOption(ArraySegment<string> arguments, ref int index, string option, float minimum, out float value, out string response)
    {
        if (!TryGetOptionValue(arguments, ref index, option, out var rawValue, out response))
        {
            value = 0f;
            return false;
        }

        if (!float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || value < minimum)
        {
            response = $"Invalid {option} value: {rawValue}";
            return false;
        }

        return true;
    }

    private static bool TryParseDistance(string value, out float result)
        => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) && result >= 0f;

    private const string Usage =
        "Usage: sl playhere <audio-file|media-url> [maxDistance] [minDistance] [options]\n" +
        "Options: --loop | --player <target> (attach/follow Transform) | --max <distance> | --min <distance> | --volume <0+> | --name <name>\n" +
        "Stop a loop: sl playhere --stop <name>\n" +
        "Examples:\n" +
        "  sl playhere music.flac --loop --player @me --max 30 --min 2 --volume 0.7 --name mymusic\n" +
        "  sl playhere https://example.com/video --max 20 --name webaudio";
}
