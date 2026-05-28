using Exiled.API.Features;
using Slafight_Plugin_EXILED.Extensions;

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
                player.ShowHint("近接チャットが<color=red>無効化</color>されました", 5f);
            }
            else
            {
                Handler.ActivatedPlayers.Add(player);
                player.ShowHint("近接チャットが<color=green>有効化</color>されました", 5f);
            }
        }
    }
}
