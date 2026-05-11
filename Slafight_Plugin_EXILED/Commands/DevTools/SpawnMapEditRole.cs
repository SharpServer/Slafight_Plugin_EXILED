using System;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using Slafight_Plugin_EXILED.Hints;

namespace Slafight_Plugin_EXILED.Commands.DevTools;

[CommandHandler(typeof(RemoteAdminCommandHandler))]
public class SpawnMapEditRole : ICommand
{
    public string Command     => "mapeditmode";
    public string[] Aliases   => ["map", "pmer", "mer", "editmap", "mp", "spawnmap"];
    public string Description => "MapEditor モードに切り替える";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!sender.CheckPermission($"slperm.{Command}"))
        {
            response = $"Permission denied. Required: slperm.{Command}";
            return false;
        }

        var player = Player.Get(sender);
        if (player == null)
        {
            response = "Player not found.";
            return false;
        }

        player.UniqueRole = "MapEditor";
        // PlayerHUD.Instance.ResetHudForPlayer(player);
        response = "Entered Map Editor Mode.";
        return true;
    }
}