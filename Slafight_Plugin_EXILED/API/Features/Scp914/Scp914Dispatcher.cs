#nullable enable
using System;
using System.Reflection;
using Exiled.API.Features;
using Exiled.API.Features.Pickups;
using Exiled.CustomItems.API.Features;
using Exiled.Events.EventArgs.Scp914;
using UnityEngine;
using Random = System.Random;

namespace Slafight_Plugin_EXILED.API.Features.Scp914;

/// <summary>
/// <see cref="Scp914Rule"/> を実際の SCP-914 イベントへ適用するエンジン。
/// 床 Pickup / インベントリアイテム両方を扱い、インベントリ満杯時の AddOrDrop も面倒を見る。
/// </summary>
public static class Scp914Dispatcher
{
    private static readonly Random Random = new();

    /// <summary>
    /// 床 Pickup のアップグレードにルールを適用する。
    /// </summary>
    /// <returns>ルールを適用したか (false = Chance を外してフォールスルー)。</returns>
    public static bool ApplyPickup(Scp914Rule rule, UpgradingPickupEventArgs? ev)
    {
        if (ev?.Pickup == null) return false;

        var ctx = new Scp914Context(ev.KnobSetting, ev.Pickup, null, null, ev.OutputPosition);

        if (!CheckChance(rule))
        {
            ev.IsAllowed = true;
            return false;
        }

        try
        {
            switch (rule.Kind)
            {
                case Scp914RuleKind.Destroy:
                    ev.IsAllowed = false;
                    ev.Pickup.Destroy();
                    break;

                case Scp914RuleKind.Keep:
                    ev.IsAllowed = false;
                    ev.Pickup.Position = ev.OutputPosition;
                    break;

                case Scp914RuleKind.Passthrough:
                    ev.IsAllowed = true;
                    break;

                case Scp914RuleKind.ToVanilla:
                {
                    ev.IsAllowed = false;
                    ev.Pickup.Destroy();
                    var type = rule.VanillaSelector?.Invoke(ctx) ?? rule.VanillaItem;
                    for (var i = 0; i < rule.Count; i++)
                        Pickup.CreateAndSpawn(type, ev.OutputPosition);
                    break;
                }

                case Scp914RuleKind.ToCustomItem:
                    ev.IsAllowed = false;
                    ev.Pickup.Destroy();
                    for (var i = 0; i < rule.Count; i++)
                        TrySpawnCustomItem(rule.CustomItemType!, ev.OutputPosition);
                    break;

                case Scp914RuleKind.ToCItem:
                    ev.IsAllowed = false;
                    ev.Pickup.Destroy();
                    for (var i = 0; i < rule.Count; i++)
                        SpawnCItem(rule.CItemType!, ev.OutputPosition);
                    break;
                
                case Scp914RuleKind.Weighted:
                {
                    ev.IsAllowed = false;
                    if (PickWeighted(rule) is { } picked)
                        ApplyPickup(picked, ev);
                    break;
                }

                case Scp914RuleKind.Custom:
                    ev.IsAllowed = false;
                    rule.CustomAction?.Invoke(ctx);
                    break;
            }
        }
        catch (Exception e)
        {
            Log.Error($"Scp914Dispatcher.ApplyPickup({rule.Kind}) failed: {e}");
        }

        return true;
    }

    /// <summary>
    /// インベントリ内アイテムのアップグレードにルールを適用する。
    /// 入力アイテムを Remove し、出力は AddOrDrop (所持者に AddItem、失敗なら床に Pickup)。
    /// </summary>
    public static bool ApplyInventory(Scp914Rule rule, UpgradingInventoryItemEventArgs? ev)
    {
        if (ev?.Player == null || ev.Item == null) return false;

        var owner = ev.Player;
        var dropPosition = owner.Position;
        var ctx = new Scp914Context(ev.KnobSetting, null, ev.Item, owner, dropPosition);

        if (!CheckChance(rule))
        {
            ev.IsAllowed = true;
            return false;
        }

        try
        {
            switch (rule.Kind)
            {
                case Scp914RuleKind.Destroy:
                    ev.IsAllowed = false;
                    owner.RemoveItem(ev.Item);
                    break;

                case Scp914RuleKind.Keep:
                    // そのまま何もしない（vanilla 処理も抑止）
                    ev.IsAllowed = false;
                    break;

                case Scp914RuleKind.Passthrough:
                    ev.IsAllowed = true;
                    break;

                case Scp914RuleKind.ToVanilla:
                {
                    ev.IsAllowed = false;
                    owner.RemoveItem(ev.Item);
                    var type = rule.VanillaSelector?.Invoke(ctx) ?? rule.VanillaItem;
                    for (var i = 0; i < rule.Count; i++)
                        AddOrDropVanilla(owner, type, dropPosition);
                    break;
                }

                case Scp914RuleKind.ToCustomItem:
                    ev.IsAllowed = false;
                    owner.RemoveItem(ev.Item);
                    for (var i = 0; i < rule.Count; i++)
                        AddOrDropCustomItem(owner, rule.CustomItemType!, dropPosition);
                    break;

                case Scp914RuleKind.ToCItem:
                    ev.IsAllowed = false;
                    owner.RemoveItem(ev.Item);
                    for (var i = 0; i < rule.Count; i++)
                        AddOrDropCItem(owner, rule.CItemType!, dropPosition);
                    break;
                
                case Scp914RuleKind.Weighted:
                {
                    ev.IsAllowed = false;
                    if (PickWeighted(rule) is { } picked)
                        ApplyInventory(picked, ev);
                    break;
                }

                case Scp914RuleKind.Custom:
                    ev.IsAllowed = false;
                    rule.CustomAction?.Invoke(ctx);
                    break;
            }
        }
        catch (Exception e)
        {
            Log.Error($"Scp914Dispatcher.ApplyInventory({rule.Kind}) failed: {e}");
        }

        return true;
    }

    // ==== Helpers ====

    private static bool CheckChance(Scp914Rule rule)
    {
        if (rule.Chance >= 1f) return true;
        if (rule.Chance <= 0f) return false;
        return Random.NextDouble() < rule.Chance;
    }

    internal static bool TrySpawnCustomItem(Type type, Vector3 position)
    {
        foreach (var item in CustomItem.Registered)
            if (type.IsInstanceOfType(item))
                return CustomItem.TrySpawn(item.Id, position, out _);
        return false;
    }

    internal static bool TryGiveCustomItem(Type type, Player player)
    {
        foreach (var item in CustomItem.Registered)
            if (type.IsInstanceOfType(item))
                return CustomItem.TryGive(player, item.Id, false);
        return false;
    }

    internal static CItem? ResolveCItem(Type type)
    {
        var method = typeof(CItem).GetMethod(nameof(CItem.Get), BindingFlags.Public | BindingFlags.Static);
        var generic = method?.MakeGenericMethod(type);
        return generic?.Invoke(null, null) as CItem;
    }

    internal static void SpawnCItem(Type type, Vector3 position)
        => ResolveCItem(type)?.Spawn(position);

    private static void AddOrDropVanilla(Player player, ItemType type, Vector3 dropPosition)
    {
        if (!player.IsInventoryFull)
        {
            var item = player.AddItem(type);
            if (item != null) return;
        }
        Pickup.CreateAndSpawn(type, dropPosition);
    }

    private static void AddOrDropCustomItem(Player player, Type customType, Vector3 dropPosition)
    {
        if (!player.IsInventoryFull && TryGiveCustomItem(customType, player)) return;
        TrySpawnCustomItem(customType, dropPosition);
    }

    private static void AddOrDropCItem(Player player, Type cItemType, Vector3 dropPosition)
    {
        var instance = ResolveCItem(cItemType);
        if (instance == null) return;

        if (!player.IsInventoryFull && instance.Give(player, true) != null) return;
        instance.Spawn(dropPosition);
    }
    
    /// <summary>
    /// WeightedEntries から相対重みで 1 つ選ぶ。
    /// 残り確率（合計 < 1 時）は null（Passthrough 相当）を返す。
    /// </summary>
    private static Scp914Rule? PickWeighted(Scp914Rule rule)
    {
        var entries = rule.WeightedEntries;
        if (entries == null || entries.Length == 0) return null;

        // 合計を算出し、正規化のベースを決める（>1 なら圧縮、≤1 なら残りは「ハズレ」）
        float total = 0f;
        foreach (var (w, _) in entries) total += w;
        float normalizer = MathF.Max(total, 1f);

        double roll = Random.NextDouble() * normalizer;
        float cursor = 0f;
        foreach (var (w, r) in entries)
        {
            cursor += w;
            if (roll < cursor) return r;
        }
        return null; // ハズレ → Passthrough 相当
    }
}
