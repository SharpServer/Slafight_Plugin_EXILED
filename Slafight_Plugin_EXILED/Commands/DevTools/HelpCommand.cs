using System;
using System.Linq;
using System.Text;
using CommandSystem;
using Exiled.Permissions.Extensions;

namespace Slafight_Plugin_EXILED.Commands.DevTools;

public class HelpCommand : ICommand
{
    private readonly Func<ICommand[]> _commands;

    public HelpCommand(Func<ICommand[]> commands)
    {
        _commands = commands;
    }

    public string Command => "help";
    public string[] Aliases { get; } = ["?", "commands"];
    public string Description => "Show Slafight command help.";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!sender.CheckPermission("slperm.help") && !sender.CheckPermission("slperm.list"))
        {
            response = "Permission denied. Required: slperm.help";
            return false;
        }

        var commands = _commands();

        if (arguments.Count >= 1)
        {
            var name = arguments.At(0);
            var command = commands.FirstOrDefault(c =>
                c.Command.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                c.Aliases.Any(a => a.Equals(name, StringComparison.OrdinalIgnoreCase)));

            if (command == null)
            {
                response = $"Unknown command: {name}";
                return false;
            }

            response =
                $"{command.Command}\n" +
                $"  Aliases: {(command.Aliases.Length == 0 ? "none" : string.Join(", ", command.Aliases))}\n" +
                $"  Permission: slperm.{command.Command}\n" +
                $"  Description: {command.Description}";
            return true;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Slafight Commands");
        sb.AppendLine("Usage: sl <command> [args]");
        sb.AppendLine("Useful first commands: sl list, sl status, sl player help, sl queue help");
        sb.AppendLine();

        foreach (var command in commands.OrderBy(c => c.Command))
        {
            if (sender.CheckPermission($"slperm.{command.Command}") ||
                command.Command is "help" or "list")
            {
                sb.AppendLine($"  {CommandTools.FormatCommand(command)}");
            }
        }

        response = sb.ToString().TrimEnd();
        return true;
    }
}
