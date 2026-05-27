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
                $"<size=24><color=#7bdcff><b>{command.Command}</b></color></size>\n" +
                $"<color=#ffb347>Aliases</color>: <color=#d7dee8>{(command.Aliases.Length == 0 ? "none" : string.Join(", ", command.Aliases))}</color>\n" +
                $"<color=#ffb347>Permission</color>: <color=#d7dee8>slperm.{command.Command}</color>\n" +
                $"<color=#ffb347>Description</color>: <color=#d7dee8>{command.Description}</color>";
            return true;
        }

        var sb = new StringBuilder();
        sb.AppendLine(CommandTools.BuildRichHeader("Slafight Help", "sl <command> [args]"));
        sb.AppendLine("<size=18><color=#9fb0c3>Useful: <color=#7bdcff>sl list</color> / <color=#7bdcff>sl status</color> / <color=#7bdcff>sl player help</color> / <color=#7bdcff>sl queue help</color></color></size>");
        sb.AppendLine("<size=18><line-height=92%>");
        sb.AppendLine(CommandTools.BuildCommandCatalog(commands, sender, true));
        sb.AppendLine("</line-height></size>");

        response = sb.ToString().TrimEnd();
        return true;
    }
}
