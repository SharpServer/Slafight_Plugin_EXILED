using System;
using Exiled.API.Features;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Extension;
using Slafight_Plugin_EXILED.API.Enums;
using HsmHint = HintServiceMeow.Core.Models.Hints.Hint;

namespace Slafight_Plugin_EXILED.Extensions;

public static class MeowExtensions
{
    public static void ShowHint(Player? player, string message, float duration = 3f) => player.ShowHsmHint(message, duration);
    public static void ShowHsmHint(
        this Player? player,
        string text,
        float duration = 3f,
        int fontSize = 24,
        float yCoordinate = 700f,
        string id = HudConstId.TemporaryHintService)
    {
        if (player == null) return;
        try
        {
            var display = player.GetPlayerDisplay();
            var oldHint = display.GetHint(id);
            if (oldHint != null)
                display.RemoveHint(oldHint);

            var hint = new HsmHint
            {
                Id = id,
                Alignment = HintAlignment.Center,
                XCoordinate = 0,
                YCoordinate = yCoordinate,
                FontSize = fontSize,
                SyncSpeed = HintSyncSpeed.Fast,
                ResolutionBasedAlign = true,
                Text = text,
                BlocksDynamicHints = false,
            };

            display.ShowHint(hint, duration);
        }
        catch (Exception ex)
        {
            Log.Debug($"[TemporaryHsmHint] failed for {player.Nickname}: {ex.Message}");
        }
    }
}
