using Exiled.API.Features;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Extension;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Hint = HintServiceMeow.Core.Models.Hints.Hint;

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
                ShowToggleHint(player, "<color=red>無効化</color>");
            }
            else
            {
                Handler.ActivatedPlayers.Add(player);
                ShowToggleHint(player, "<color=green>有効化</color>");
            }
        }
    }

    private static void ShowToggleHint(Player player, string stateText)
    {
        var keybindHint = ServerSpecificUserSettings.BuildKeybindUsageHint(
            player,
            ServerSpecifics.ProximityChatKeybindSettingId,
            "近接チャットをON/OFFできます");

        var display = player.GetPlayerDisplay();
        var oldHint = display.GetHint(HudConstId.TemporaryHintService);
        if (oldHint != null)
            display.RemoveHint(oldHint);

        var hint = new Hint
        {
            Id = HudConstId.TemporaryHintService,
            Alignment = HintAlignment.Center,
            XCoordinate = 0,
            YCoordinate = 700,
            FontSize = 24,
            SyncSpeed = HintSyncSpeed.Fast,
            ResolutionBasedAlign = true,
            BlocksDynamicHints = false,
            Text = $"近接チャットが{stateText}されました\n{keybindHint.Text}",
            Parameters = keybindHint.Parameters,
        };

        display.ShowHint(hint, 5f);
    }
}
