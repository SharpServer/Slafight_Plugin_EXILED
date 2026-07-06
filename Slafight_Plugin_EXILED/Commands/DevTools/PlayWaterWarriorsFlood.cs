using System;
using CommandSystem;
using Exiled.Permissions.Extensions;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.SpecialEvents.Events;

namespace Slafight_Plugin_EXILED.Commands.DevTools;

public class PlayWaterWarriorsFlood : ICommand
{
    public string Command => "playwaterflood";
    public string[] Aliases { get; } = ["waterflood", "playflood", "p_ww", "p_2"];
    public string Description => "Play Water Warriors facility flood scene.";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!sender.CheckPermission($"slperm.{Command}"))
        {
            response = $"You don't have permission to execute this command. Required permission: slperm.{Command}";
            return false;
        }

        if (SpecialEvent.GetEvent(SpecialEventType.WaterWarriorsRaid) is not WaterWarriorsRaidEvent waterRaid)
        {
            response = "WaterWarriorsRaidEvent is not registered.";
            return false;
        }

        if (!waterRaid.TryPlayFloodScene(out string failureReason))
        {
            response = failureReason;
            return false;
        }

        response = "Water Warriors flood scene started.";
        return true;
    }
}
