#nullable enable
using Exiled.API.Features;
using InventorySystem.Items.Pickups;
using ProjectMER.Features;
using ProjectMER.Features.Serializable;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Interface;
using UnityEngine;
using Pickup = Exiled.API.Features.Pickups.Pickup;
using Player = LabApi.Features.Wrappers.Player;

namespace Slafight_Plugin_EXILED.CustomMaps.Bridges;

/// <summary>
/// ProjectMER の ItemSpawnpoint カスタムアイテムレジストリへ CItem プロバイダを登録するブリッジ。
/// ItemSpawnpoint 側の統一 Item 指定（"名前" / "(CItem)名前"）はここを経由して CItem に解決される。
/// </summary>
public class ItemSpawnpointCItemBridge : IBootstrapHandler
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
            return;

        ItemSpawnpointCustomItemRegistry.Register(TrySpawnCItem, TryGiveCItem);
        _registered = true;
        Log.Info("[ItemSpawnpointCItemBridge] Registered CItem provider for ProjectMER ItemSpawnpoints.");
    }

    public static void Unregister()
    {
        if (!_registered)
            return;

        ItemSpawnpointCustomItemRegistry.Unregister(TrySpawnCItem, TryGiveCItem);
        _registered = false;
    }

    private static bool TrySpawnCItem(
        string customItemKey,
        SerializableItemSpawnpoint spawnpoint,
        Vector3 position,
        Quaternion rotation,
        Transform parent,
        out ItemPickupBase? pickupBase)
    {
        pickupBase = null;

        if (!CItem.TryResolve(customItemKey, out CItem? cItem) || cItem == null)
            return false;

        var pickup = cItem.Spawn(position);
        if (pickup == null)
            return false;

        try
        {
            ApplySpawnpointSettings(pickup, spawnpoint, rotation, parent);
            pickupBase = pickup.Base;
            if (pickupBase != null)
                return true;

            Log.Warn($"[ItemSpawnpointCItemBridge] CItem '{customItemKey}' spawned without an ItemPickupBase.");
            pickup.Destroy();
            return false;
        }
        catch
        {
            pickup.Destroy();
            throw;
        }
    }

    private static bool TryGiveCItem(string customItemKey, ItemPickupBase pickup, Player player)
    {
        if (!CItem.TryResolve(customItemKey, out CItem? cItem) || cItem == null)
            return false;

        var exiledPlayer = Exiled.API.Features.Player.Get(player.ReferenceHub);
        return cItem.Give(exiledPlayer, displayMessage: true) != null;
    }

    private static void ApplySpawnpointSettings(
        Pickup pickup,
        SerializableItemSpawnpoint spawnpoint,
        Quaternion rotation,
        Transform parent)
    {
        pickup.Rotation = rotation;
        pickup.Transform.parent = parent;

        if (spawnpoint.Scale != Vector3.one)
            pickup.Scale = Vector3.Scale(pickup.Scale, spawnpoint.Scale);

        if (spawnpoint.Weight != -1)
            pickup.Weight = spawnpoint.Weight;

        if (pickup.Rigidbody != null)
            pickup.Rigidbody.isKinematic = !spawnpoint.UseGravity;

        pickup.IsLocked = !spawnpoint.CanBePickedUp;
    }
}
