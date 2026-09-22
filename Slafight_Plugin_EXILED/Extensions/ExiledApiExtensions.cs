using ExiledItem = Exiled.API.Features.Items.Item;
using ExiledPickup = Exiled.API.Features.Pickups.Pickup;
using ExiledPlayer = Exiled.API.Features.Player;
using LabItem = LabApi.Features.Wrappers.Item;
using LabPickup = LabApi.Features.Wrappers.Pickup;
using LabPlayer = LabApi.Features.Wrappers.Player;

namespace Slafight_Plugin_EXILED.Extensions;

/// <summary>LabAPI のラッパーから EXILED API へ戻るための橋渡しです。</summary>
public static class ExiledApiExtensions
{
    public static ExiledItem AsExiled(this LabItem item) =>
        item?.Base is { } baseItem ? ExiledItem.Get(baseItem) : null;

    public static ExiledPickup AsExiled(this LabPickup pickup) =>
        pickup?.Base is { } basePickup ? ExiledPickup.Get(basePickup) : null;

    public static ExiledPlayer AsExiled(this LabPlayer player) =>
        player?.ReferenceHub is { } hub ? ExiledPlayer.Get(hub) : null;

    /// <summary>LabAPI の Pickup から EXILED が保持する直前の所有者を取得します。</summary>
    public static ExiledPlayer GetPreviousOwner(this LabPickup pickup) =>
        pickup.AsExiled()?.PreviousOwner;
}
