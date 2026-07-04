using System;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using Slafight_Plugin_EXILED.Hints;
using Slafight_Plugin_EXILED.MainHandlers;

namespace Slafight_Plugin_EXILED.Commands.DevTools;

public class SpawnDebugToolRole : ICommand
{
    public string Command => "debugmode";
    public string[] Aliases { get; } = ["debug","spawndebug"];
    public string Description => "Enable DebugMode and prepare the Debug HUD, including during WaitingForPlayers.";
    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!sender.CheckPermission($"slperm.{Command}"))
        {
            response = $"You don't have permission to execute this command. Required permission: slperm.{Command}";
            return false;
        }

        Player player = Player.Get(sender);
        if (player == null)
        {
            response = "Player sender not found. Run this command from Remote Admin as an in-game player.";
            return false;
        }

        if (!player.CheckPermission(PlayerPermissions.Noclip))
        {
            response = "DebugMode requires the Noclip permission.";
            return false;
        }

        player.UniqueRole = "Debug";
        DebugModeHandler.SetDebugMode(player, true);

        bool hudReady = PlayerHUD.Instance?.ForceDebugHudSync(player, logException: true) ?? false;
        if (!hudReady)
        {
            response = "DebugMode enabled, but Debug HUD could not be prepared yet.";
            return false;
        }

        response = "DebugMode enabled and Debug HUD prepared.";
        return true;
    }
}
