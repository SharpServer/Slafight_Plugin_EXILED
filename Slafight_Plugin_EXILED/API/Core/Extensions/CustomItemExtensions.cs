using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Features;

using ExiledItem = Exiled.API.Features.Items.Item;
using ExiledPickup = Exiled.API.Features.Pickups.Pickup;
using LabItem = LabApi.Features.Wrappers.Item;
using LabPickup = LabApi.Features.Wrappers.Pickup;

namespace Slafight_Plugin_EXILED.API.Core.Extensions;

/// <summary>
/// アイテム、ピックアップ、プレイヤーから追跡中の <see cref="CustomItem"/> を
/// 直接判定・取得するための糖衣です。
/// </summary>
/// <example>
/// <code>
/// if (item.TryGetCustomItem&lt;MediPistol&gt;(out var pistol))
///     pistol.Refill();
///
/// if (player.TryGetCurrentCustomItem(out CustomItem custom))
///     Log.Info(custom.Name);
///
/// if (player.HasCustomItem&lt;MediPistol&gt;(out var ownedPistol))
///     ownedPistol.Refill();
/// </code>
/// </example>
public static class CustomItemExtensions
{
    // EXILED Item

    /// <summary>対応するカスタムアイテムです。追跡対象でなければ null。</summary>
    public static CustomItem GetCustomItem(this ExiledItem item) =>
        item is null ? null : CustomItem.Of(item.Serial);

    /// <summary>対応するカスタムアイテムを指定型として返します。型が違えば null。</summary>
    public static T GetCustomItem<T>(this ExiledItem item) where T : CustomItem =>
        item is null ? null : CustomItem.Of<T>(item.Serial);

    /// <summary>対応するカスタムアイテムがあれば取り出します。</summary>
    public static bool TryGetCustomItem(this ExiledItem item, out CustomItem customItem) =>
        CustomItem.TryGet(item?.Serial ?? 0, out customItem);

    /// <summary>対応するカスタムアイテムが指定型なら取り出します。</summary>
    public static bool TryGetCustomItem<T>(this ExiledItem item, out T customItem) where T : CustomItem =>
        CustomItem.TryGet(item?.Serial ?? 0, out customItem);

    /// <summary>何らかのカスタムアイテムかどうか。</summary>
    public static bool IsCustomItem(this ExiledItem item) => CustomItem.Of(item?.Serial ?? 0) is not null;

    /// <summary>何らかのカスタムアイテムなら、そのインスタンスを返します。</summary>
    public static bool IsCustomItem(this ExiledItem item, out CustomItem customItem) =>
        item.TryGetCustomItem(out customItem);

    /// <summary>指定型のカスタムアイテムかどうか。派生型も真になります。</summary>
    public static bool IsCustomItem<T>(this ExiledItem item) where T : CustomItem =>
        CustomItem.Is<T>(item?.Serial ?? 0);

    /// <summary>指定型のカスタムアイテムなら、型付けされたインスタンスを返します。</summary>
    public static bool IsCustomItem<T>(this ExiledItem item, out T customItem) where T : CustomItem =>
        item.TryGetCustomItem(out customItem);

    /// <summary>実行時に指定した型のカスタムアイテムかどうか。派生型も真になります。</summary>
    public static bool IsCustomItem(this ExiledItem item, Type customItemType) =>
        CustomItem.Is(item?.Serial ?? 0, customItemType, out _);

    // EXILED Pickup

    /// <inheritdoc cref="GetCustomItem(ExiledItem)"/>
    public static CustomItem GetCustomItem(this ExiledPickup pickup) =>
        pickup is null ? null : CustomItem.Of(pickup.Serial);

    /// <inheritdoc cref="GetCustomItem{T}(ExiledItem)"/>
    public static T GetCustomItem<T>(this ExiledPickup pickup) where T : CustomItem =>
        pickup is null ? null : CustomItem.Of<T>(pickup.Serial);

    /// <inheritdoc cref="TryGetCustomItem(ExiledItem, out CustomItem)"/>
    public static bool TryGetCustomItem(this ExiledPickup pickup, out CustomItem customItem) =>
        CustomItem.TryGet(pickup?.Serial ?? 0, out customItem);

    /// <inheritdoc cref="TryGetCustomItem{T}(ExiledItem, out T)"/>
    public static bool TryGetCustomItem<T>(this ExiledPickup pickup, out T customItem) where T : CustomItem =>
        CustomItem.TryGet(pickup?.Serial ?? 0, out customItem);

    /// <inheritdoc cref="IsCustomItem(ExiledItem)"/>
    public static bool IsCustomItem(this ExiledPickup pickup) => CustomItem.Of(pickup?.Serial ?? 0) is not null;

    /// <inheritdoc cref="IsCustomItem(ExiledItem, out CustomItem)"/>
    public static bool IsCustomItem(this ExiledPickup pickup, out CustomItem customItem) =>
        pickup.TryGetCustomItem(out customItem);

    /// <inheritdoc cref="IsCustomItem{T}(ExiledItem)"/>
    public static bool IsCustomItem<T>(this ExiledPickup pickup) where T : CustomItem =>
        CustomItem.Is<T>(pickup?.Serial ?? 0);

    /// <inheritdoc cref="IsCustomItem{T}(ExiledItem, out T)"/>
    public static bool IsCustomItem<T>(this ExiledPickup pickup, out T customItem) where T : CustomItem =>
        pickup.TryGetCustomItem(out customItem);

    /// <inheritdoc cref="IsCustomItem(ExiledItem, Type)"/>
    public static bool IsCustomItem(this ExiledPickup pickup, Type customItemType) =>
        CustomItem.Is(pickup?.Serial ?? 0, customItemType, out _);

    // LabAPI Item / Pickup

    /// <inheritdoc cref="GetCustomItem(ExiledItem)"/>
    public static CustomItem GetCustomItem(this LabItem item) => CustomItem.Of(item);

    /// <inheritdoc cref="GetCustomItem{T}(ExiledItem)"/>
    public static T GetCustomItem<T>(this LabItem item) where T : CustomItem => CustomItem.Of<T>(item);

    /// <inheritdoc cref="TryGetCustomItem(ExiledItem, out CustomItem)"/>
    public static bool TryGetCustomItem(this LabItem item, out CustomItem customItem) =>
        CustomItem.TryGet(item, out customItem);

    /// <inheritdoc cref="TryGetCustomItem{T}(ExiledItem, out T)"/>
    public static bool TryGetCustomItem<T>(this LabItem item, out T customItem) where T : CustomItem =>
        CustomItem.TryGet(item, out customItem);

    /// <inheritdoc cref="IsCustomItem(ExiledItem)"/>
    public static bool IsCustomItem(this LabItem item) => CustomItem.Of(item) is not null;

    /// <inheritdoc cref="IsCustomItem(ExiledItem, out CustomItem)"/>
    public static bool IsCustomItem(this LabItem item, out CustomItem customItem) =>
        item.TryGetCustomItem(out customItem);

    /// <inheritdoc cref="IsCustomItem{T}(ExiledItem)"/>
    public static bool IsCustomItem<T>(this LabItem item) where T : CustomItem => CustomItem.Is<T>(item?.Serial ?? 0);

    /// <inheritdoc cref="IsCustomItem{T}(ExiledItem, out T)"/>
    public static bool IsCustomItem<T>(this LabItem item, out T customItem) where T : CustomItem =>
        item.TryGetCustomItem(out customItem);

    /// <inheritdoc cref="IsCustomItem(ExiledItem, Type)"/>
    public static bool IsCustomItem(this LabItem item, Type customItemType) =>
        CustomItem.Is(item, customItemType, out _);

    /// <inheritdoc cref="GetCustomItem(ExiledItem)"/>
    public static CustomItem GetCustomItem(this LabPickup pickup) => CustomItem.Of(pickup);

    /// <inheritdoc cref="GetCustomItem{T}(ExiledItem)"/>
    public static T GetCustomItem<T>(this LabPickup pickup) where T : CustomItem => CustomItem.Of<T>(pickup);

    /// <inheritdoc cref="TryGetCustomItem(ExiledItem, out CustomItem)"/>
    public static bool TryGetCustomItem(this LabPickup pickup, out CustomItem customItem) =>
        CustomItem.TryGet(pickup, out customItem);

    /// <inheritdoc cref="TryGetCustomItem{T}(ExiledItem, out T)"/>
    public static bool TryGetCustomItem<T>(this LabPickup pickup, out T customItem) where T : CustomItem =>
        CustomItem.TryGet(pickup, out customItem);

    /// <inheritdoc cref="IsCustomItem(ExiledItem)"/>
    public static bool IsCustomItem(this LabPickup pickup) => CustomItem.Of(pickup) is not null;

    /// <inheritdoc cref="IsCustomItem(ExiledItem, out CustomItem)"/>
    public static bool IsCustomItem(this LabPickup pickup, out CustomItem customItem) =>
        pickup.TryGetCustomItem(out customItem);

    /// <inheritdoc cref="IsCustomItem{T}(ExiledItem)"/>
    public static bool IsCustomItem<T>(this LabPickup pickup) where T : CustomItem =>
        CustomItem.Is<T>(pickup?.Serial ?? 0);

    /// <inheritdoc cref="IsCustomItem{T}(ExiledItem, out T)"/>
    public static bool IsCustomItem<T>(this LabPickup pickup, out T customItem) where T : CustomItem =>
        pickup.TryGetCustomItem(out customItem);

    /// <inheritdoc cref="IsCustomItem(ExiledItem, Type)"/>
    public static bool IsCustomItem(this LabPickup pickup, Type customItemType) =>
        CustomItem.Is(pickup, customItemType, out _);

    // Player

    /// <summary>現在手に持っているカスタムアイテムです。無ければ null。</summary>
    public static CustomItem GetCurrentCustomItem(this Player player) => player?.CurrentItem.GetCustomItem();

    /// <summary>現在手に持っているカスタムアイテムを指定型として返します。</summary>
    public static T GetCurrentCustomItem<T>(this Player player) where T : CustomItem =>
        player?.CurrentItem.GetCustomItem<T>();

    /// <summary>現在手に持っているものがカスタムアイテムなら取り出します。</summary>
    public static bool TryGetCurrentCustomItem(this Player player, out CustomItem customItem) =>
        CustomItem.TryGet(player?.CurrentItem?.Serial ?? 0, out customItem);

    /// <summary>現在手に持っているものが指定型なら取り出します。</summary>
    public static bool TryGetCurrentCustomItem<T>(this Player player, out T customItem) where T : CustomItem =>
        CustomItem.TryGet(player?.CurrentItem?.Serial ?? 0, out customItem);

    /// <summary>現在手に持っているものが何らかのカスタムアイテムかどうか。</summary>
    public static bool IsCurrentCustomItem(this Player player) => player?.CurrentItem.IsCustomItem() == true;

    /// <summary>現在手に持っているものがカスタムアイテムなら、そのインスタンスを返します。</summary>
    public static bool IsCurrentCustomItem(this Player player, out CustomItem customItem) =>
        player.TryGetCurrentCustomItem(out customItem);

    /// <summary>現在手に持っているものが指定型のカスタムアイテムかどうか。</summary>
    public static bool IsCurrentCustomItem<T>(this Player player) where T : CustomItem =>
        player?.CurrentItem.IsCustomItem<T>() == true;

    /// <summary>現在手に持っているものが指定型なら、型付けされたインスタンスを返します。</summary>
    public static bool IsCurrentCustomItem<T>(this Player player, out T customItem) where T : CustomItem =>
        player.TryGetCurrentCustomItem(out customItem);

    /// <summary>プレイヤーが所持している、追跡中のカスタムアイテムを列挙します。</summary>
    public static IEnumerable<CustomItem> GetCustomItems(this Player player) =>
        player?.Items?.Select(GetCustomItem).Where(custom => custom is not null) ?? Enumerable.Empty<CustomItem>();

    /// <summary>プレイヤーが所持している、指定型のカスタムアイテムを列挙します。</summary>
    public static IEnumerable<T> GetCustomItems<T>(this Player player) where T : CustomItem =>
        player?.Items?.Select(GetCustomItem<T>).Where(custom => custom is not null) ?? Enumerable.Empty<T>();

    /// <summary>プレイヤーが何らかのカスタムアイテムを所持しているかどうか。</summary>
    public static bool HasCustomItem(this Player player) => player?.Items?.Any(IsCustomItem) == true;

    /// <summary>プレイヤーが何らかのカスタムアイテムを所持していれば、最初のものを返します。</summary>
    public static bool HasCustomItem(this Player player, out CustomItem customItem)
    {
        customItem = player.GetCustomItems().FirstOrDefault();
        return customItem is not null;
    }

    /// <summary>プレイヤーが指定型のカスタムアイテムを所持しているかどうか。</summary>
    public static bool HasCustomItem<T>(this Player player) where T : CustomItem =>
        player?.Items?.Any(IsCustomItem<T>) == true;

    /// <summary>プレイヤーが指定型を所持していれば、最初のインスタンスを返します。</summary>
    public static bool HasCustomItem<T>(this Player player, out T customItem) where T : CustomItem
    {
        customItem = player.GetCustomItems<T>().FirstOrDefault();
        return customItem is not null;
    }
}
