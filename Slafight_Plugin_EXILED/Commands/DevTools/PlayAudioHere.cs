using System;
using System.Globalization;
using System.IO;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using Slafight_Plugin_EXILED.API.Features;

namespace Slafight_Plugin_EXILED.Commands.DevTools;

public class PlayAudioHere : ICommand
{
    public string Command => "playhere";
    public string[] Aliases { get; } = ["playaudiohere", "pah"];
    public string Description => "Play an audio file from AudioReferences at your current position.";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!sender.CheckPermission($"slperm.{Command}"))
        {
            response = $"You don't have permission to execute this command. Required permission: slperm.{Command}";
            return false;
        }

        var executor = Player.Get(sender);
        if (executor == null)
        {
            response = "Player not found. Run this command from Remote Admin as an in-game player.";
            return false;
        }

        if (arguments.Count < 1)
        {
            response = "Usage: sl playhere <file.ogg> [maxDistance] [minDistance]";
            return false;
        }

        var fileName = arguments.At(0);
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

        var maxDistance = 15f;
        var minDistance = 1f;

        if (arguments.Count >= 2 && !TryParseDistance(arguments.At(1), out maxDistance))
        {
            response = $"Invalid maxDistance: {arguments.At(1)}";
            return false;
        }

        if (arguments.Count >= 3 && !TryParseDistance(arguments.At(2), out minDistance))
        {
            response = $"Invalid minDistance: {arguments.At(2)}";
            return false;
        }

        minDistance = Math.Max(1f, minDistance);

        if (minDistance > maxDistance)
        {
            response = "minDistance cannot be greater than maxDistance.";
            return false;
        }

        try
        {
            var audioPlayerName = $"PlayHere_{executor.Id}_{DateTime.UtcNow.Ticks}";
            SpeakerApi.Play(fileName, audioPlayerName, executor.Position, true, null, true, maxDistance, minDistance);
            response = $"Playing {fileName} at {executor.Nickname}'s position. Range: {minDistance:0.##}-{maxDistance:0.##}";
            return true;
        }
        catch (Exception ex)
        {
            response = $"Failed to play {fileName}: {ex.Message}";
            return false;
        }
    }

    private static bool TryParseDistance(string value, out float result)
        => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) && result >= 0f;
}
