using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Features;

namespace Slafight_Plugin_EXILED.ProximityChat;

public class ActivateHandler
{
    public static void ToggleProximityChat(Player player)
    {
        if (player == null)
            return;

        if (Handler.CanPlayerUseProximityChat(player))
        {
            if (Handler.ActivatedPlayers.Contains(player))
            {
                Handler.ActivatedPlayers.Remove(player);
                player.ShowHint(BuildToggleHint(player, "<color=red>無効化</color>"), 5f);
            }
            else
            {
                Handler.ActivatedPlayers.Add(player);
                player.ShowHint(BuildToggleHint(player, "<color=green>有効化</color>"), 5f);
            }
        }
    }

    private static string BuildToggleHint(Player player, string stateText)
        => $"近接チャットが{stateText}されました\n" +
           ServerSpecificUserSettings.BuildKeybindUsageHint(
               player,
               ServerSpecifics.ProximityChatKeybindSettingId,
               "近接チャットをON/OFFできます");
}
