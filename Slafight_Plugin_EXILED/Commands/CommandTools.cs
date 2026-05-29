using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;

namespace Slafight_Plugin_EXILED.Commands;

internal static class CommandTools
{
    public static bool CheckPermission(ICommandSender sender, string command, out string response)
    {
        var permission = $"slperm.{command}";
        if (sender.CheckPermission(permission))
        {
            response = string.Empty;
            return true;
        }

        response = $"Permission denied. Required: {permission}";
        return false;
    }

    public static bool TryGetExecutor(ICommandSender sender, out Player player, out string response)
    {
        player = Player.Get(sender);
        if (player != null)
        {
            response = string.Empty;
            return true;
        }

        response = "Player sender not found. Run this command from Remote Admin as an in-game player.";
        return false;
    }

    public static bool TryResolvePlayer(string input, Player executor, out Player player, out string response)
    {
        player = null;
        response = string.Empty;

        if (string.IsNullOrWhiteSpace(input) ||
            input.Equals("@me", StringComparison.OrdinalIgnoreCase) ||
            input.Equals("me", StringComparison.OrdinalIgnoreCase) ||
            input.Equals("self", StringComparison.OrdinalIgnoreCase))
        {
            player = executor;
            return true;
        }

        if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
            player = Player.Get(id);

        player ??= Player.List.FirstOrDefault(p =>
            p.UserId.Equals(input, StringComparison.OrdinalIgnoreCase) ||
            p.Nickname.Equals(input, StringComparison.OrdinalIgnoreCase));

        player ??= Player.List.FirstOrDefault(p =>
            p.Nickname.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0);

        if (player != null)
            return true;

        response = $"Player not found: {input}";
        return false;
    }

    public static bool TryResolveOptionalPlayer(
        ArraySegment<string> arguments,
        int index,
        Player executor,
        out Player target,
        out string response)
    {
        target = executor;
        response = string.Empty;

        if (arguments.Count <= index)
            return true;

        return TryResolvePlayer(arguments.At(index), executor, out target, out response);
    }

    public static bool TryParseFloat(string value, out float result)
        => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    public static string JoinNames(IEnumerable<string> names, string filter = null)
    {
        var query = names;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            query = query.Where(n => n.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        return string.Join(", ", query.OrderBy(n => n));
    }

    public static string BuildSection(string title, IEnumerable<string> lines)
    {
        var sb = new StringBuilder();
        sb.AppendLine(title);
        foreach (var line in lines)
            sb.AppendLine($"  {line}");

        return sb.ToString().TrimEnd();
    }

    public static string FormatCommand(ICommand command)
    {
        var aliases = command.Aliases is { Length: > 0 }
            ? $" [{string.Join(", ", command.Aliases)}]"
            : string.Empty;

        return $"{command.Command}{aliases} - {command.Description}";
    }

    public static string BuildCommandCatalog(
        IEnumerable<ICommand> commands,
        ICommandSender sender,
        bool alwaysShowHelpAndList = false)
    {
        var visibleCommands = commands
            .Where(c => sender.CheckPermission($"slperm.{c.Command}") ||
                        alwaysShowHelpAndList && c.Command is "help" or "list")
            .OrderBy(c => GetCommandCategory(c.Command))
            .ThenBy(c => c.Command)
            .ToArray();

        var sb = new StringBuilder();

        foreach (var group in visibleCommands.GroupBy(c => GetCommandCategory(c.Command)))
        {
            sb.AppendLine();
            sb.AppendLine($"<color=#ffb347><b>{GetCommandCategoryTitle(group.Key)}</b></color>");

            foreach (var command in group)
                sb.AppendLine(FormatRichCommand(command));
        }

        return sb.ToString().TrimEnd();
    }

    public static string FormatRichCommand(ICommand command)
    {
        var aliases = command.Aliases is { Length: > 0 }
            ? $" <color=#8fa3b8>[{string.Join(", ", command.Aliases)}]</color>"
            : string.Empty;

        return
            $"  <color=#7bdcff><b>{command.Command}</b></color>{aliases}\n" +
            $"    <color=#d7dee8>{NormalizeForSingleLine(command.Description)}</color>";
    }

    public static string BuildRichHeader(string title, string usage)
    {
        return
            $"<size=26><b><color=#ffa31a>{title}</color></b></size>\n" +
            $"<size=18><color=#c7d0dd>Usage: <color=#7bdcff>{usage}</color>  |  Details: <color=#7bdcff>sl help <command></color></color></size>";
    }

    private static int GetCommandCategory(string command)
    {
        return command switch
        {
            "help" or "list" or "status" or "restart" => 0,
            "player" or "spawn" or "giveitem" or "giveability" => 1,
            "queue" or "getqueue" or "addqueue" or "setqueue" or "rerollqueue" or "rerollspecial" or "run" => 2,
            "debugstart" or "debugmode" or "objprefab" or "hitbox" or "prefab" or "spawnwave" => 3,
            "proximity" or "voicerec" or "playsurfaceattack" or "surfacebombinginstant" or "playhere" or "playomega" or "activategenerator" => 4,
            _ => 5
        };
    }

    private static string GetCommandCategoryTitle(int category)
    {
        return category switch
        {
            0 => "Core",
            1 => "Players / Content",
            2 => "Events / Queue",
            3 => "Map / Debug",
            4 => "World / Effects",
            _ => "Other"
        };
    }

    private static string NormalizeForSingleLine(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return string.Join(" ", value
            .Replace("\r", "\n")
            .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim()));
    }
}
