using System;
using CommandSystem;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Features.FilmmakerAnimations;

namespace Slafight_Plugin_EXILED.Commands.DevTools;

public sealed class FilmmakerAnimationCommand : ICommand
{
    public string Command => "filmmaker";
    public string[] Aliases { get; } = ["film", "fmanim"];
    public string Description => "Save and play Filmmaker camera animations.";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!CommandTools.CheckPermission(sender, Command, out response))
            return false;

        if (!CommandTools.TryGetExecutor(sender, out Player executor, out response))
            return false;

        if (arguments.Count == 0 || arguments.At(0).Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            response =
                "Usage:\n" +
                "  sl filmmaker save <name>\n" +
                "  sl filmmaker play <name> [target] [absolute|relative] [speed] [loop|once] [lock|nolock]\n" +
                "  sl filmmaker stop [target|all]\n" +
                "  sl filmmaker list [filter]\n" +
                "  sl filmmaker info <name>\n" +
                $"Animations directory: {FilmmakerAnimationStorage.DirectoryPath}";
            return false;
        }

        switch (arguments.At(0).ToLowerInvariant())
        {
            case "save":
                return Save(arguments, executor, out response);

            case "play":
                return Play(arguments, executor, out response);

            case "stop":
                return Stop(arguments, executor, out response);

            case "list":
            case "ls":
                return List(arguments, out response);

            case "info":
                return Info(arguments, out response);

            default:
                response = $"Unknown filmmaker action: {arguments.At(0)}. Use `sl filmmaker help`.";
                return false;
        }
    }

    private static bool Save(ArraySegment<string> arguments, Player executor, out string response)
    {
        if (arguments.Count < 2)
        {
            response = "Usage: sl filmmaker save <name>";
            return false;
        }

        if (!FilmmakerAnimationStorage.TryNormalizeName(arguments.At(1), out string name, out response))
            return false;

        FilmmakerAnimationClip clip = FilmmakerAnimationClip.FromCurrentTimeline(name);
        clip.CreatedBy = $"{executor.Nickname} ({executor.UserId})";

        if (!clip.HasPlayableMotion)
        {
            response =
                "No Filmmaker position or rotation keyframes were found in the server timeline.\n" +
                "Dedicated servers cannot read keyframes that only exist in a player's local Filmmaker UI.";
            return false;
        }

        if (!FilmmakerAnimationStorage.TrySave(clip, out string path, out response))
            return false;

        string roleWarning = executor.Role.Type == RoleTypeId.Filmmaker
            ? string.Empty
            : "\nWarning: executor is not currently the Filmmaker role; saved the global server timeline.";

        response =
            $"Saved Filmmaker animation '{clip.Name}' to {path}\n" +
            $"Frames {clip.FirstFrame}-{clip.LastFrame} ({clip.DurationSeconds:0.##}s), " +
            $"Position={clip.Positions.Count}, Rotation={clip.Rotations.Count}, Zoom={clip.Zooms.Count}." +
            roleWarning;
        return true;
    }

    private static bool Play(ArraySegment<string> arguments, Player executor, out string response)
    {
        if (arguments.Count < 2)
        {
            response = "Usage: sl filmmaker play <name> [target] [absolute|relative] [speed] [loop|once] [lock|nolock]";
            return false;
        }

        string name = arguments.At(1);
        Player target = executor;
        int optionStart = 2;

        if (arguments.Count >= 3 &&
            !IsPlaybackOption(arguments.At(2)) &&
            !CommandTools.TryParseFloat(arguments.At(2), out _))
        {
            if (!CommandTools.TryResolvePlayer(arguments.At(2), executor, out target, out response))
                return false;

            optionStart = 3;
        }

        if (!TryParseOptions(arguments, optionStart, out FilmmakerPlaybackOptions options, out response))
            return false;

        return FilmmakerAnimationPlayer.TryPlay(target, name, options, out response);
    }

    private static bool Stop(ArraySegment<string> arguments, Player executor, out string response)
    {
        if (arguments.Count >= 2 && arguments.At(1).Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            FilmmakerAnimationPlayer.StopAll();
            response = "Stopped all Filmmaker animation playback.";
            return true;
        }

        if (!CommandTools.TryResolveOptionalPlayer(arguments, 1, executor, out Player target, out response))
            return false;

        return FilmmakerAnimationPlayer.Stop(target, out response);
    }

    private static bool List(ArraySegment<string> arguments, out string response)
    {
        string filter = arguments.Count >= 2 ? arguments.At(1) : null;
        var animations = FilmmakerAnimationStorage.GetAnimationNames(filter);
        response = animations.Count > 0
            ? "Filmmaker animations:\n  " + string.Join("\n  ", animations)
            : $"No Filmmaker animations found in {FilmmakerAnimationStorage.DirectoryPath}.";
        return true;
    }

    private static bool Info(ArraySegment<string> arguments, out string response)
    {
        if (arguments.Count < 2)
        {
            response = "Usage: sl filmmaker info <name>";
            return false;
        }

        if (!FilmmakerAnimationStorage.TryLoad(arguments.At(1), out FilmmakerAnimationClip clip, out response))
            return false;

        response =
            $"Filmmaker animation '{clip.Name}'\n" +
            $"  Path: {FilmmakerAnimationStorage.GetFilePath(clip.Name)}\n" +
            $"  CreatedUtc: {clip.CreatedUtc:O}\n" +
            $"  CreatedBy: {(string.IsNullOrWhiteSpace(clip.CreatedBy) ? "unknown" : clip.CreatedBy)}\n" +
            $"  Frames: {clip.FirstFrame}-{clip.LastFrame} ({clip.DurationSeconds:0.##}s @ {clip.EffectiveFrameRate:0.##}fps)\n" +
            $"  Tracks: Position={clip.Positions.Count}, Rotation={clip.Rotations.Count}, Zoom={clip.Zooms.Count}\n" +
            "  Playback note: normal FPC players receive position plus yaw/pitch. Roll and zoom are stored but not forced by vanilla FPC.";
        return true;
    }

    private static bool TryParseOptions(
        ArraySegment<string> arguments,
        int start,
        out FilmmakerPlaybackOptions options,
        out string response)
    {
        options = new FilmmakerPlaybackOptions();
        response = string.Empty;

        for (int i = start; i < arguments.Count; i++)
        {
            string option = arguments.At(i);
            switch (option.ToLowerInvariant())
            {
                case "absolute":
                case "abs":
                case "world":
                    options.Mode = FilmmakerPlaybackMode.Absolute;
                    continue;

                case "relative":
                case "rel":
                case "local":
                    options.Mode = FilmmakerPlaybackMode.Relative;
                    continue;

                case "loop":
                    options.Loop = true;
                    continue;

                case "once":
                    options.Loop = false;
                    continue;

                case "lock":
                    options.LockMovement = true;
                    continue;

                case "nolock":
                    options.LockMovement = false;
                    continue;
            }

            if (CommandTools.TryParseFloat(option, out float speed))
            {
                if (speed < 0.05f || speed > 8f)
                {
                    response = "Playback speed must be between 0.05 and 8.";
                    return false;
                }

                options.SpeedScale = speed;
                continue;
            }

            response = $"Unknown playback option: {option}";
            return false;
        }

        return true;
    }

    private static bool IsPlaybackOption(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string normalized = value.ToLowerInvariant();
        return normalized is
            "absolute" or "abs" or "world" or
            "relative" or "rel" or "local" or
            "loop" or "once" or "lock" or "nolock";
    }
}
