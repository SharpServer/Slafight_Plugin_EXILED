#nullable enable
using System;
using System.Collections.Generic;
using Exiled.CustomItems.API.Features;
using Slafight_Plugin_EXILED.Changes;

namespace Slafight_Plugin_EXILED.API.Features.Scp914;

/// <summary>
/// SCP-914 アップグレードルールの一元管理レジストリ。
/// vanilla <see cref="ItemType"/> / Exiled <see cref="CustomItem"/> / <see cref="CItem"/>
/// すべての変換ルールをここに登録し、<see cref="Scp914Changes"/> が一括でディスパッチする。
/// </summary>
public static class Scp914Registry
{
    /// <summary>
    /// 全アイテム共通の「入口ロール」。null なら無効化。
    /// これに当選した場合、通常の変換ルールは評価されない。
    /// </summary>
    public static Scp914Rule? WildcardRule { get; set; }
    public static Scp914Rule? O5WildcardRule;

    private static readonly Dictionary<ItemType, Scp914RuleSet> VanillaTable = new();
    private static readonly Dictionary<Type, Scp914RuleSet> CustomItemTable = new();
    private static readonly Dictionary<Type, Scp914RuleSet> CItemTable = new();

    // ==== Register ====

    public static void RegisterVanilla(ItemType type, Scp914RuleSet rules)
        => VanillaTable[type] = rules;

    public static void RegisterCustomItem<T>(Scp914RuleSet rules) where T : CustomItem
        => CustomItemTable[typeof(T)] = rules;

    public static void RegisterCItem<T>(Scp914RuleSet rules) where T : CItem
        => CItemTable[typeof(T)] = rules;

    // ==== Lookup ====

    public static bool TryGetVanilla(ItemType type, out Scp914RuleSet? ruleSet)
    {
        if (VanillaTable.TryGetValue(type, out var found))
        {
            ruleSet = found;
            return true;
        }
        ruleSet = null;
        return false;
    }

    public static bool TryGetForCustomItem(CustomItem customItem, out Scp914RuleSet? ruleSet)
    {
        var type = customItem.GetType();
        if (CustomItemTable.TryGetValue(type, out var found))
        {
            ruleSet = found;
            return true;
        }
        foreach (var kv in CustomItemTable)
        {
            if (kv.Key.IsInstanceOfType(customItem))
            {
                ruleSet = kv.Value;
                return true;
            }
        }
        ruleSet = null;
        return false;
    }

    public static bool TryGetForCItem(CItem cItem, out Scp914RuleSet? ruleSet)
    {
        var type = cItem.GetType();
        if (CItemTable.TryGetValue(type, out var found))
        {
            ruleSet = found;
            return true;
        }
        foreach (var kv in CItemTable)
        {
            if (kv.Key.IsInstanceOfType(cItem))
            {
                ruleSet = kv.Value;
                return true;
            }
        }
        ruleSet = null;
        return false;
    }

    /// <summary>全テーブルをクリア (プラグイン終了時用)。</summary>
    public static void Clear()
    {
        WildcardRule = null;
        VanillaTable.Clear();
        CustomItemTable.Clear();
        CItemTable.Clear();
    }
}
