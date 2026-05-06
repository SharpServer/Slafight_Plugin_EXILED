#nullable enable
using System;
using Exiled.CustomItems.API.Features;

namespace Slafight_Plugin_EXILED.API.Features.Scp914;

public enum Scp914RuleKind
{
    Destroy,
    Keep,
    Passthrough,
    ToVanilla,
    ToCustomItem,
    ToCItem,
    Custom,
    Weighted,   // ← 追加
}

public readonly struct Scp914Rule
{
    public Scp914RuleKind Kind { get; init; }
    public float Chance { get; init; }
    public int Count { get; init; }

    public ItemType VanillaItem { get; init; }
    public Func<Scp914Context, ItemType>? VanillaSelector { get; init; }

    public Type? CustomItemType { get; init; }
    public Type? CItemType { get; init; }

    public Action<Scp914Context>? CustomAction { get; init; }

    /// <summary>Weighted 用エントリ。(相対重み, 子ルール) のペア配列。</summary>
    public (float Weight, Scp914Rule Rule)[]? WeightedEntries { get; init; }

    // ==== Static factories ====

    public static Scp914Rule Destroy     => new() { Kind = Scp914RuleKind.Destroy,      Chance = 1f, Count = 1 };
    public static Scp914Rule Keep        => new() { Kind = Scp914RuleKind.Keep,          Chance = 1f, Count = 1 };
    public static Scp914Rule Passthrough => new() { Kind = Scp914RuleKind.Passthrough,   Chance = 1f, Count = 1 };

    public static Scp914Rule ToVanilla(ItemType type, int count = 1) => new()
        { Kind = Scp914RuleKind.ToVanilla, VanillaItem = type, Count = count, Chance = 1f };

    public static Scp914Rule ToVanilla(Func<Scp914Context, ItemType> selector, int count = 1) => new()
        { Kind = Scp914RuleKind.ToVanilla, VanillaSelector = selector, Count = count, Chance = 1f };

    public static Scp914Rule ToCustomItem<T>(int count = 1) where T : CustomItem => new()
        { Kind = Scp914RuleKind.ToCustomItem, CustomItemType = typeof(T), Count = count, Chance = 1f };

    public static Scp914Rule ToCItem<T>(int count = 1) where T : CItem => new()
        { Kind = Scp914RuleKind.ToCItem, CItemType = typeof(T), Count = count, Chance = 1f };

    public static Scp914Rule Custom(Action<Scp914Context> action) => new()
        { Kind = Scp914RuleKind.Custom, CustomAction = action, Chance = 1f, Count = 1 };

    /// <summary>
    /// 相対重みで子ルールを抽選する。
    /// 重みの合計が 1 未満の場合、残り確率は「何もしない (Passthrough)」扱い。
    /// 合計が 1 を超える場合は自動正規化。
    /// </summary>
    public static Scp914Rule Weighted(params (float Weight, Scp914Rule Rule)[] entries) => new()
    {
        Kind            = Scp914RuleKind.Weighted,
        WeightedEntries = entries,
        Chance          = 1f,
        Count           = 1,
    };

    // ==== Chain modifiers ====

    public Scp914Rule WithChance(float chance) => this with { Chance = chance };
    public Scp914Rule Times(int count)         => this with { Count  = count  };
}