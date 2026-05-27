using Exiled.API.Features;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.ProximityChat;

// 変更後（コンストラクタ＆イベントハンドラを削除）
public class ActivateHandler
{
    // ここには「近接チャットのオン/オフだけ」を行うメソッドだけ残す
    public static void ToggleProximityChat(Player player)
    {
        if (player == null)
            return;

        if (Handler.CanPlayerUseProximityChat(player))
        {
            if (Handler.ActivatedPlayers.Contains(player))
            {
                Handler.ActivatedPlayers.Remove(player);
                Handler.DestroyProximitySpeaker(player);
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
