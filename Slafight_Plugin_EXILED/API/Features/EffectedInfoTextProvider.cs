using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using MEC;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.Hints;

namespace Slafight_Plugin_EXILED.API.Features;

public static class EffectedInfoTextProvider
{
    private static readonly Dictionary<int, int> Versions = new();

    public static void Set(Player player, string? text, float duration = 0f)
    {
        if (player == null) return;

        var playerId = player.Id;
        var version = Versions.GetValueOrDefault(player.Id) + 1;
        Versions[player.Id] = version;

        PlayerHUD.Instance?.HintSync(SyncType.PHUD_EffectedInfo, text ?? string.Empty, player);

        if (duration <= 0f)
            return;

        Timing.CallDelayed(duration, () =>
        {
            var currentPlayer = Player.Get(playerId);
            if (currentPlayer == null || currentPlayer.ReferenceHub == null) return;
            if (!Versions.TryGetValue(playerId, out var current) || current != version) return;

            Clear(currentPlayer);
        });
    }

    public static void Clear(Player player)
    {
        if (player == null) return;

        Versions.Remove(player.Id);
        PlayerHUD.Instance?.HintSync(SyncType.PHUD_EffectedInfo, string.Empty, player);
    }

    public static void ClearAll()
    {
        foreach (var player in Player.List)
        {
            if (player == null || !player.IsConnected) continue;
            PlayerHUD.Instance?.HintSync(SyncType.PHUD_EffectedInfo, string.Empty, player);
        }

        Versions.Clear();
    }
}
