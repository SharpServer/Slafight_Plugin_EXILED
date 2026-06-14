using System.Collections.Generic;
using System.Linq;
using System.Text;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.Hints;

namespace Slafight_Plugin_EXILED.API.Features;

public static class AbilityManager
{
    public static readonly Dictionary<int, AbilityLoadout> Loadouts = new();

    // 「必ず作る」用
    public static AbilityLoadout? GetOrCreateLoadout(Player? player)
    {
        if (!TryGetPlayerId(player, out var playerId))
            return null;

        if (!Loadouts.TryGetValue(playerId, out var loadout))
        {
            loadout = new AbilityLoadout();
            Loadouts[playerId] = loadout;
        }

        return loadout;
    }

    // 「あれば取るだけ」用
    public static bool TryGetLoadout(Player? player, out AbilityLoadout loadout)
    {
        loadout = null!;
        return TryGetPlayerId(player, out var playerId) &&
               Loadouts.TryGetValue(playerId, out loadout);
    }

    // 現在選択中のアビリティ使用可能か
    public static bool CanUseActiveAbility(Player? player)
        => TryGetPlayerId(player, out var playerId) &&
           AbilityBase.CanUseSelectedAbility(playerId);

    // スロット切り替え
    public static bool SwitchToSlot(Player? player, int slotIndex)
    {
        if (!TryGetLoadout(player, out var loadout))
            return false;

        if (slotIndex is < 0 or >= AbilityLoadout.MaxSlots)
            return false;

        if (loadout.Slots[slotIndex] == null)
            return false;

        loadout.ActiveIndex = slotIndex;
        UpdateAbilityHint(player, loadout);
        return true;
    }

    // 次スロットへ
    public static bool NextSlot(Player? player)
    {
        if (!TryGetLoadout(player, out var loadout))
            return false;

        if (!loadout.CycleNext())
            return false;

        UpdateAbilityHint(player, loadout);
        return true;
    }

    // ★公開メソッド：HUD更新のみ
    public static void UpdateAbilityHint(Player? player, AbilityLoadout? loadout)
    {
        if (!CanSyncHud(player) || loadout == null)
            return;

        PlayerHUD.Instance?.ForceAbilityHudSync(player!);
    }

    // プレイヤー全クリア
    public static void ClearPlayer(Player? player)
    {
        if (!TryGetPlayerId(player, out var playerId))
            return;

        AbilityBase.RevokeAbility(playerId);
        Loadouts.Remove(playerId);

        if (CanSyncHud(player))
            PlayerHUD.Instance?.ForceAbilityHudSync(player!);
    }

    // 全員クリア
    public static void ClearAllLoadouts()
    {
        AbilityBase.RevokeAllPlayers();
        Loadouts.Clear();
    }

    // スロットだけクリア
    public static void ClearSlots(Player? player)
    {
        if (!TryGetLoadout(player, out var loadout))
            return;

        for (int i = 0; i < AbilityLoadout.MaxSlots; i++)
            loadout.Slots[i] = null;

        loadout.ActiveIndex = 0;
        UpdateAbilityHint(player, loadout);
    }

    // イベント管理
    private static bool _initialized;

    internal static void RegisterEvents()
    {
        if (_initialized) return;

        Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;
        Exiled.Events.Handlers.Server.WaitingForPlayers += OnWaitingForPlayers;
        Exiled.Events.Handlers.Player.Left += OnPlayerLeft;
        Exiled.Events.Handlers.Player.Joined += OnPlayerJoined;
        _initialized = true;
    }

    internal static void UnregisterEvents()
    {
        if (!_initialized) return;

        Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;
        Exiled.Events.Handlers.Server.WaitingForPlayers -= OnWaitingForPlayers;
        Exiled.Events.Handlers.Player.Left -= OnPlayerLeft;
        Exiled.Events.Handlers.Player.Joined -= OnPlayerJoined;
        ClearAllLoadouts();
        _initialized = false;
    }

    private static void OnRoundStarted()
    {
        foreach (var playerId in Loadouts.Keys.ToArray())
        {
            var player = Player.Get(playerId);
            if (player?.ReferenceHub != null)
                AbilityBase.ResetCooldown(playerId);
        }
    }

    private static void OnWaitingForPlayers()
    {
        ClearAllLoadouts();
    }

    private static void OnPlayerLeft(LeftEventArgs ev)
    {
        ClearPlayer(ev.Player);
    }

    private static void OnPlayerJoined(JoinedEventArgs ev)
    {
        GetOrCreateLoadout(ev.Player);
    }

    // デバッグ用
    public static string GetLoadoutInfo(Player? player)
    {
        if (!TryGetLoadout(player, out var loadout))
            return "No loadout";

        var sb = new StringBuilder();
        sb.AppendLine($"Active: {loadout.ActiveIndex}");
        for (int i = 0; i < AbilityLoadout.MaxSlots; i++)
        {
            var ability = loadout.Slots[i];
            sb.AppendLine($"Slot{i}: {ability?.GetType().Name ?? "空"}");
        }
        return sb.ToString();
    }

    private static bool TryGetPlayerId(Player? player, out int playerId)
    {
        playerId = 0;

        try
        {
            if (player == null)
                return false;

            playerId = player.Id;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool CanSyncHud(Player? player)
    {
        try
        {
            return player != null &&
                   player.ReferenceHub != null &&
                   player.IsConnected &&
                   !player.IsNPC;
        }
        catch
        {
            return false;
        }
    }
}
