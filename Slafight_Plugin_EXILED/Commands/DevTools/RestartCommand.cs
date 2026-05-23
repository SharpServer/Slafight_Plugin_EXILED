using System;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;

namespace Slafight_Plugin_EXILED.Commands.DevTools;

public class RestartCommand : ICommand
{
    public string Command => "restart";
    public string[] Aliases { get; } = [];
    public string Description => "Restart Server but restart round";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!sender.CheckPermission($"slperm.{Command}"))
        {
            response = $"Permission denied. Required: slperm.{Command}";
            return false;
        }

        Round.Restart(overrideRestartAction: true, restartAction: ServerStatic.NextRoundAction.Restart);
        response = "Executed successfully!";
        return true;
    }
}
