using System;
using System.Collections.Generic;
using Exiled.API.Features;
using InventorySystem.Items;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// プレイヤーのカテゴリ所持上限へ、複数の独立した加算値を安全に積む API です。
/// </summary>
/// <remarks>
/// 有効上限は、ゲーム設定とアーマー補正へ、この API のスタックと
/// <see cref="CustomItem.IgnoreCategoryLimits"/> の自己除外枠を加えて求めます。
/// 戻された <see cref="IDisposable"/> を破棄すれば、その加算だけが外れます。
/// </remarks>
public static class ItemLimitApi
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<uint, Dictionary<ItemCategory, Dictionary<long, int>>> Contributions = new();
    private static long nextContributionId;

    [ThreadStatic]
    private static List<ushort> pickupChecks;

    /// <summary>
    /// 指定プレイヤーのカテゴリ上限へ加算値を積みます。
    /// 負数を渡せば一時的な減算としても利用できます。
    /// </summary>
    public static IDisposable PushCategoryLimit(Player player, ItemCategory category, int amount = 1)
    {
        if (player?.ReferenceHub == null)
            throw new ArgumentNullException(nameof(player));
        if (category == ItemCategory.None)
            throw new ArgumentOutOfRangeException(nameof(category), "None カテゴリへ上限は設定できません。");
        if (amount == 0)
            return EmptyLease.Instance;

        uint playerNetId = player.NetId;
        long contributionId;
        lock (SyncRoot)
        {
            contributionId = ++nextContributionId;
            if (!Contributions.TryGetValue(playerNetId, out Dictionary<ItemCategory, Dictionary<long, int>> byCategory))
                Contributions[playerNetId] = byCategory = new();
            if (!byCategory.TryGetValue(category, out Dictionary<long, int> entries))
                byCategory[category] = entries = new();

            entries[contributionId] = amount;
        }

        return new LimitLease(playerNetId, category, contributionId);
    }

    /// <summary>この API から積まれている加算値の合計を返します。</summary>
    public static int GetStackedBonus(Player player, ItemCategory category) =>
        player == null ? 0 : GetStackedBonus(player.NetId, category);

    internal static sbyte ApplyCategoryLimit(sbyte originalLimit, ReferenceHub hub, ItemCategory category)
    {
        if (hub == null || category == ItemCategory.None)
            return originalLimit;

        int bonus = GetStackedBonus(hub.netId, category) + CountIgnoredItems(hub, category);

        ushort pendingSerial = CurrentPickupSerial;
        if (pendingSerial != 0 &&
            CustomItem.Of(pendingSerial) is { IgnoreCategoryLimits: true, Pickup: { } pickup } &&
            pickup.Category == category)
        {
            bonus++;
        }

        if (bonus == 0)
            return originalLimit;

        // InventoryLimits は負数にも Abs を掛けるため、符号ではなく絶対値側へ加算する。
        int adjusted = originalLimit < 0 ? originalLimit - bonus : originalLimit + bonus;
        return (sbyte)Math.Max(sbyte.MinValue, Math.Min(sbyte.MaxValue, adjusted));
    }

    internal static void EnterPickupCheck(ushort serial)
    {
        pickupChecks ??= new List<ushort>();
        pickupChecks.Add(serial);
    }

    internal static void ExitPickupCheck()
    {
        if (pickupChecks is { Count: > 0 })
            pickupChecks.RemoveAt(pickupChecks.Count - 1);
    }

    internal static void Clear(Player player)
    {
        if (player != null)
            Clear(player.NetId);
    }

    internal static void ClearAll()
    {
        lock (SyncRoot)
            Contributions.Clear();
    }

    private static ushort CurrentPickupSerial =>
        pickupChecks is { Count: > 0 } ? pickupChecks[pickupChecks.Count - 1] : (ushort)0;

    private static int CountIgnoredItems(ReferenceHub hub, ItemCategory category)
    {
        int count = 0;
        foreach (CustomItem custom in CustomItem.Tracked)
        {
            if (!custom.IgnoreCategoryLimits || custom.Owner?.ReferenceHub != hub)
                continue;
            if (custom.Item is { } item && item.Category == category && item.Base.CountsTowardsCategoryLimit)
                count++;
        }

        return count;
    }

    private static int GetStackedBonus(uint playerNetId, ItemCategory category)
    {
        lock (SyncRoot)
        {
            if (!Contributions.TryGetValue(playerNetId, out Dictionary<ItemCategory, Dictionary<long, int>> byCategory) ||
                !byCategory.TryGetValue(category, out Dictionary<long, int> entries))
            {
                return 0;
            }

            int total = 0;
            foreach (int amount in entries.Values)
                total += amount;
            return total;
        }
    }

    private static void Clear(uint playerNetId)
    {
        lock (SyncRoot)
            Contributions.Remove(playerNetId);
    }

    private static void Remove(uint playerNetId, ItemCategory category, long contributionId)
    {
        lock (SyncRoot)
        {
            if (!Contributions.TryGetValue(playerNetId, out Dictionary<ItemCategory, Dictionary<long, int>> byCategory) ||
                !byCategory.TryGetValue(category, out Dictionary<long, int> entries))
            {
                return;
            }

            entries.Remove(contributionId);
            if (entries.Count == 0)
                byCategory.Remove(category);
            if (byCategory.Count == 0)
                Contributions.Remove(playerNetId);
        }
    }

    private sealed class LimitLease(uint playerNetId, ItemCategory category, long contributionId) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            Remove(playerNetId, category, contributionId);
        }
    }

    private sealed class EmptyLease : IDisposable
    {
        public static readonly EmptyLease Instance = new();
        public void Dispose()
        {
        }
    }
}

/// <summary>切断・ラウンド遷移時に外部から積まれた上限スタックを掃除します。</summary>
public sealed class ItemLimitApiHandler : EventHandlerBase
{
    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Player.Left += OnLeft;
        Exiled.Events.Handlers.Server.WaitingForPlayers += ItemLimitApi.ClearAll;
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Player.Left -= OnLeft;
        Exiled.Events.Handlers.Server.WaitingForPlayers -= ItemLimitApi.ClearAll;
        ItemLimitApi.ClearAll();
    }

    private static void OnLeft(Exiled.Events.EventArgs.Player.LeftEventArgs ev) => ItemLimitApi.Clear(ev.Player);
}
