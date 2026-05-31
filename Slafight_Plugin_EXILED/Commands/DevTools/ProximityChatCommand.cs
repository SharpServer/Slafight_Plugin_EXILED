using System;
using CommandSystem;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.ProximityChat;

namespace Slafight_Plugin_EXILED.Commands.DevTools;

public class ProximityChatCommand : ICommand
{
    public string Command => "proximity";
    public string[] Aliases { get; } = ["prox", "pchat"];
    public string Description => "Override or toggle ProximityChat for a player.";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!CommandTools.CheckPermission(sender, Command, out response))
            return false;

        if (!CommandTools.TryGetExecutor(sender, out var executor, out response))
            return false;

        if (arguments.Count < 1)
        {
            response = "Usage: sl proximity <force|unforce|toggle|status> [player]";
            return false;
        }

        var action = arguments.At(0).ToLowerInvariant();
        if (!CommandTools.TryResolveOptionalPlayer(arguments, 1, executor, out var target, out response))
            return false;

        switch (action)
        {
            case "force":
            case "on":
            case "enable":
                Handler.SetProximityChatForced(target, true);
MeowExtensions.ShowHint(                target, "近接チャットが<color=green>強制有効化</color>されました", 5f);
                response = $"Forced ProximityChat for {target.Nickname}.";
                return true;

            case "unforce":
            case "off":
            case "disable":
                Handler.SetProximityChatForced(target, false);
MeowExtensions.ShowHint(                target, "近接チャットの<color=yellow>強制有効化</color>を解除しました", 5f);
                response = $"Removed ProximityChat override for {target.Nickname}.";
                return true;

            case "toggle":
                ActivateHandler.ToggleProximityChat(target);
                response = $"Toggled ProximityChat for {target.Nickname}. Active={Handler.ActivatedPlayers.Contains(target)} Forced={Handler.IsProximityChatForced(target)}";
                return true;

            case "status":
                response =
                    $"ProximityChat status for {target.Nickname}: " +
                    $"CanUse={Handler.CanPlayerUseProximityChat(target)}, " +
                    $"Active={Handler.ActivatedPlayers.Contains(target)}, " +
                    $"Forced={Handler.IsProximityChatForced(target)}";
                return true;

            default:
                response = "Usage: sl proximity <force|unforce|toggle|status> [player]";
                return false;
        }
    }
}
