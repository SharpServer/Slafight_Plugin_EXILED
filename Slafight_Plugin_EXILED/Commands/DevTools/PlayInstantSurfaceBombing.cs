using System;
using CommandSystem;
using Exiled.Permissions.Extensions;
using Slafight_Plugin_EXILED.CustomMaps.FacilityControlRoomFunctions;

namespace Slafight_Plugin_EXILED.Commands.DevTools;

public class PlayInstantSurfaceBombing : ICommand
{
    public string Command => "surfacebombinginstant";
    public string[] Aliases { get; } = ["sbi", "instantbombing", "surfacebombing"];
    public string Description => "Start surface bombing immediately.";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!sender.CheckPermission($"slperm.{Command}"))
        {
            response = $"You don't have permission to execute this command. Required permission: slperm.{Command}";
            return false;
        }

        if (!SurfaceBombingFunction.TryStartInstantBombing(out var failureReason))
        {
            response = failureReason;
            return false;
        }

        response = "Instant surface bombing started.";
        return true;
    }
}
