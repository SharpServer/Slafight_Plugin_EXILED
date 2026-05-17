using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.Hints;

namespace Slafight_Plugin_EXILED.API.Features;

public static class AbilityManager
{
    public static readonly Dictionary<int, AbilityLoadout> Loadouts = new();

    // 「必ず作る」用
    public static AbilityLoadout GetOrCreateLoadout(Player player)
    {
        if (!Loadouts.TryGetValue(player.Id, out var loadout))
        {
            loadout = new AbilityLoadout();
            Loadouts[player.Id] = loadout;
        }
        return loadout;
    }

    // 「あれば取るだけ」用
    public static bool TryGetLoadout(Player player, out AbilityLoadout loadout)
        => Loadouts.TryGetValue(player.Id, out loadout);

    // 現在選択中のアビリティ使用可能か
    public static bool CanUseActiveAbility(Player player)
        => AbilityBase.CanUseSelectedAbility(player.Id);

    // スロット切り替え
    public static bool SwitchToSlot(Player player, int slotIndex)
    {
        if (!TryGetLoadout(player, out var loadout))
            return false;

        if (slotIndex is < 0 or >= AbilityLoadout.MaxSlots)
            return false;

        loadout.ActiveIndex = slotIndex;
        // UpdateAbilityHint(player, loadout);
        return true;
    }

    // 次スロットへ
    public static bool NextSlot(Player player)
    {
        if (!TryGetLoadout(player, out var loadout))
            return false;

        loadout.CycleNext();
        // UpdateAbilityHint(player, loadout);
        return true;
    }

    // ★公開メソッド：HUD更新のみ
    public static void UpdateAbilityHint(Player player, AbilityLoadout loadout)
    {
        // 表示はPlayerHUDのループに任せる。即時反映が必要な場合のみ呼ぶ
        // PlayerHUD.Instance?.ForceAbilityHudSync(player);
    }

    // プレイヤー全クリア
    public static void ClearPlayer(Player player)
    {
        if (player == null)
            return;

        AbilityBase.RevokeAbility(player.Id);
        Loadouts.Remove(player.Id);
    }

    // 全員クリア
    public static void ClearAllLoadouts()
    {
        foreach (var kvp in Loadouts.ToArray())
        {
            AbilityBase.RevokeAbility(kvp.Key);
            Loadouts.Remove(kvp.Key);
        }

        Loadouts.Clear();
    }

    // スロットだけクリア
    public static void ClearSlots(Player player)
    {
        if (!TryGetLoadout(player, out var loadout))
            return;

        for (int i = 0; i < AbilityLoadout.MaxSlots; i++)
            loadout.Slots[i] = null;

        loadout.ActiveIndex = 0;
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
        foreach (var kvp in Loadouts)
        {
            var player = Player.Get(kvp.Key);
            if (player?.IsConnected == true)
                AbilityBase.ResetCooldown(player.Id);
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
    public static string GetLoadoutInfo(Player player)
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
}
