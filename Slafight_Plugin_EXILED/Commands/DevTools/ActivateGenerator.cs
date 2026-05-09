using System;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;

namespace Slafight_Plugin_EXILED.Commands.DevTools;

public class ActivateGenerator : ICommand
{
    public string Command => "activategenerator";
    public string[] Aliases { get; } = ["generator_allstart"];
    public string Description => "Start activating all generator.";
    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!sender.CheckPermission($"slperm.{Command}"))
        {
            response = $"You don't have permission to execute this command. Required permission: mpr.{Command}";
            return false;
        }
        var executor = Player.Get(sender);
        if (executor == null)
        {
            response = "Executed Player not found.";
            return false;
        }

        foreach (var generator in Generator.List)
        {
            if (generator.IsEngaged || generator.IsActivating) continue;
            generator.IsEngaged = true;
        }

        response = ("Successfully!");
        return true;
    }
}