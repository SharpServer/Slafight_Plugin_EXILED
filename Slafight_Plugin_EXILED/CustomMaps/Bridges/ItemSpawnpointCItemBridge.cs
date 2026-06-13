#nullable enable
using System;
using System.Linq;
using System.Reflection;
using Exiled.API.Features;
using InventorySystem.Items.Pickups;
using ProjectMER.Features.Serializable;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Interface;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps.Bridges;

/// <summary>
/// Registers Slafight CItem support with ProjectMER's ItemSpawnpoint custom item registry.
/// </summary>
public class ItemSpawnpointCItemBridge : IBootstrapHandler
{
    private const string RegistryTypeName = "ProjectMER.Features.ItemSpawnpointCustomItemRegistry, ProjectMER";
    private static readonly MethodInfo SpawnMethod = typeof(ItemSpawnpointCItemBridge).GetMethod(
        nameof(TrySpawnCItem),
        BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly MethodInfo GiveMethod = typeof(ItemSpawnpointCItemBridge).GetMethod(
        nameof(TryGiveCItem),
        BindingFlags.Static | BindingFlags.NonPublic)!;

    private static Type? _registryType;
    private static Delegate? _spawnDelegate;
    private static Delegate? _giveDelegate;

    public static void Register()
    {
        if (_spawnDelegate != null || _giveDelegate != null)
            return;

        _registryType = Type.GetType(RegistryTypeName);
        if (_registryType == null)
        {
            Log.Warn("[ItemSpawnpointCItemBridge] ProjectMER custom ItemSpawnpoint registry was not found. Update ProjectMER to enable CItem ItemSpawnpoints.");
            return;
        }

        MethodInfo? registerMethod = GetRegistryMethod("Register");
        if (registerMethod == null)
        {
            Log.Warn("[ItemSpawnpointCItemBridge] ProjectMER custom ItemSpawnpoint registry has no compatible Register method.");
            return;
        }

        ParameterInfo[] parameters = registerMethod.GetParameters();
        _spawnDelegate = Delegate.CreateDelegate(parameters[0].ParameterType, SpawnMethod);
        _giveDelegate = Delegate.CreateDelegate(parameters[1].ParameterType, GiveMethod);
        registerMethod.Invoke(null, [_spawnDelegate, _giveDelegate]);
        Log.Info("[ItemSpawnpointCItemBridge] Registered CItem provider for ProjectMER ItemSpawnpoints.");
    }

    public static void Unregister()
    {
        if (_registryType == null || _spawnDelegate == null || _giveDelegate == null)
            return;

        MethodInfo? unregisterMethod = GetRegistryMethod("Unregister");
        unregisterMethod?.Invoke(null, [_spawnDelegate, _giveDelegate]);

        _spawnDelegate = null;
        _giveDelegate = null;
        _registryType = null;
    }

    private static MethodInfo? GetRegistryMethod(string name)
        => _registryType?
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(method => method.Name == name && method.GetParameters().Length == 2);

    private static bool TrySpawnCItem(
        string customItemKey,
        SerializableItemSpawnpoint spawnpoint,
        Vector3 position,
        Quaternion rotation,
        Transform parent,
        out ItemPickupBase? pickupBase)
    {
        pickupBase = null;

        if (!CItem.TryGetByKey(customItemKey, out CItem? cItem) || cItem == null)
            return false;

        var pickup = cItem.Spawn(position);
        if (pickup == null)
            return false;

        ApplySpawnpointSettings(pickup, spawnpoint, rotation, parent);
        pickupBase = TryGetPickupBase(pickup);
        if (pickupBase != null)
            return true;

        Log.Warn($"[ItemSpawnpointCItemBridge] CItem '{customItemKey}' spawned without an ItemPickupBase.");
        pickup.Destroy();
        return false;
    }

    private static bool TryGiveCItem(string customItemKey, ItemPickupBase pickup, LabApi.Features.Wrappers.Player player)
    {
        if (!CItem.TryGetByKey(customItemKey, out CItem? cItem) || cItem == null)
            return false;

        var exiledPlayer = Exiled.API.Features.Player.Get(player.ReferenceHub);
        return cItem.Give(exiledPlayer, displayMessage: true) != null;
    }

    private static void ApplySpawnpointSettings(
        Exiled.API.Features.Pickups.Pickup pickup,
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

    private static ItemPickupBase? TryGetPickupBase(Exiled.API.Features.Pickups.Pickup pickup)
    {
        PropertyInfo? baseProperty = pickup.GetType().GetProperty("Base", BindingFlags.Public | BindingFlags.Instance);
        return baseProperty?.GetValue(pickup) as ItemPickupBase;
    }
}
