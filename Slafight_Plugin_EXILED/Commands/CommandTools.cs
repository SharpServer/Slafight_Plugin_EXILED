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
}
