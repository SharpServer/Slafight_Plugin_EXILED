#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Exiled.API.Features;
using Exiled.API.Features.Pickups;
using InventorySystem.Items.Pickups;
using MEC;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using ProjectMER.Features.Serializable;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Interface;
using UnityEngine;
using YamlDotNet.RepresentationModel;
using PlayerEvents = Exiled.Events.EventArgs.Player;

namespace Slafight_Plugin_EXILED.CustomMaps.Bridges;

/// <summary>
/// Replaces ProjectMER ItemSpawnpoints annotated with a CItem key with tracked Slafight CItem pickups.
/// </summary>
public class ItemSpawnpointCItemBridge : SlafightLabApiHandler, IBootstrapHandler
{
    private static readonly string[] ItemSpawnpointKeys =
    [
        "item_spawnpoints",
        "item_spawn_points",
        "ItemSpawnpoints",
        "ItemSpawnPoints",
    ];

    private static readonly string[] CItemKeyFields =
    [
        "c_item_key",
        "citem_key",
        "c_item",
        "citem",
        "custom_item_key",
        "custom_item",
        "slafight_c_item",
    ];

    private static ItemSpawnpointCItemBridge _instance = null!;
    private static readonly Dictionary<string, SpawnpointBinding> Bindings = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<ushort, CItemUseState> MultiUsePickups = new();
    private static readonly Dictionary<string, MapCItemConfigCache> MapConfigCache = new(StringComparer.OrdinalIgnoreCase);
    private static CoroutineHandle _scanCoroutine;

    public static void Register() => _instance = LabApiHandlerRegistry.Register(_instance);

    public static void Unregister()
    {
        ClearBindings();
        if (_scanCoroutine.IsRunning)
            Timing.KillCoroutines(_scanCoroutine);

        LabApiHandlerRegistry.Unregister(ref _instance);
    }

    protected override void RegisterEvents(EventSubscriptionScope subscriptions)
    {
        subscriptions.Add(
            () => LabApi.Events.Handlers.ServerEvents.WaitingForPlayers += OnWaitingForPlayers,
            () => LabApi.Events.Handlers.ServerEvents.WaitingForPlayers -= OnWaitingForPlayers);
        subscriptions.Add(
            () => LabApi.Events.Handlers.ServerEvents.RoundStarted += OnRoundStarted,
            () => LabApi.Events.Handlers.ServerEvents.RoundStarted -= OnRoundStarted);
        subscriptions.Add(
            () => LabApi.Events.Handlers.ServerEvents.RoundRestarted += OnRoundRestarted,
            () => LabApi.Events.Handlers.ServerEvents.RoundRestarted -= OnRoundRestarted);
        subscriptions.Add(
            () => Exiled.Events.Handlers.Player.PickingUpItem += OnPickingUpItem,
            () => Exiled.Events.Handlers.Player.PickingUpItem -= OnPickingUpItem);
    }

    protected override void OnDisposed()
    {
        ClearBindings();
        if (_scanCoroutine.IsRunning)
            Timing.KillCoroutines(_scanCoroutine);
    }

    private static void OnWaitingForPlayers()
        => EnsureScanCoroutine();

    private static void OnRoundStarted()
        => EnsureScanCoroutine();

    private static void OnRoundRestarted()
    {
        ClearBindings();
        MapConfigCache.Clear();
    }

    private static void EnsureScanCoroutine()
    {
        if (_scanCoroutine.IsRunning)
            return;

        _scanCoroutine = Timing.RunCoroutine(ScanCoroutine());
    }

    private static IEnumerator<float> ScanCoroutine()
    {
        while (true)
        {
            ApplyConfiguredSpawnpoints();
            yield return Timing.WaitForSeconds(1f);
        }
    }

    private static void ApplyConfiguredSpawnpoints()
    {
        HashSet<string> seenSpawnpoints = new(StringComparer.OrdinalIgnoreCase);
        int spawned = 0;

        foreach (MapSchematic map in MapUtils.LoadedMaps.Values)
        {
            IReadOnlyDictionary<string, string> cItemKeys = GetConfiguredCItemKeys(map.Name);
            if (cItemKeys.Count == 0)
                continue;

            foreach (MapEditorObject mapObject in map.SpawnedObjects.ToList())
            {
                if (mapObject == null || mapObject.Base is not SerializableItemSpawnpoint spawnpoint)
                    continue;

                if (!cItemKeys.TryGetValue(mapObject.Id, out string cItemKey) || string.IsNullOrWhiteSpace(cItemKey))
                    continue;

                string bindingKey = GetBindingKey(map.Name, mapObject.Id);
                seenSpawnpoints.Add(bindingKey);

                if (Bindings.TryGetValue(bindingKey, out SpawnpointBinding binding))
                {
                    if (binding.Matches(map.Name, mapObject, cItemKey, spawnpoint))
                    {
                        binding.SyncPickups(mapObject.transform, spawnpoint);
                        RemoveVanillaPickups(mapObject, binding);
                        continue;
                    }

                    DestroyBinding(bindingKey);
                }

                if (!CItem.TryGetByKey(cItemKey, out CItem? cItem) || cItem == null)
                {
                    Log.Warn($"[ItemSpawnpointCItemBridge] Unknown CItem key '{cItemKey}' for {map.Name}:{mapObject.Id}.");
                    continue;
                }

                RemoveVanillaPickups(mapObject);

                SpawnpointBinding newBinding = SpawnCItems(mapObject, spawnpoint, cItemKey, cItem);
                if (newBinding.Pickups.Count == 0)
                    continue;

                Bindings[bindingKey] = newBinding;
                spawned += newBinding.Pickups.Count;
            }
        }

        RemoveMissingBindings(seenSpawnpoints);

        if (spawned > 0)
            Log.Info($"[ItemSpawnpointCItemBridge] Spawned {spawned} CItem pickup(s) from ProjectMER ItemSpawnpoints.");
    }

    private static SpawnpointBinding SpawnCItems(
        MapEditorObject mapObject,
        SerializableItemSpawnpoint spawnpoint,
        string cItemKey,
        CItem cItem)
    {
        List<Pickup> pickups = [];
        int count = Math.Max(1, (int)spawnpoint.NumberOfItems);
        for (int i = 0; i < count; i++)
        {
            Pickup? pickup = cItem.Spawn(mapObject.transform.position);
            if (pickup == null)
                continue;

            ApplySpawnpointSettings(pickup, mapObject.transform, spawnpoint, applyScale: true);
            pickups.Add(pickup);

            if (spawnpoint.NumberOfUses > 1)
                MultiUsePickups[pickup.Serial] = new CItemUseState(cItem, spawnpoint.NumberOfUses);
        }

        return new SpawnpointBinding(mapObject, cItemKey, spawnpoint, pickups);
    }

    private static void ApplySpawnpointSettings(
        Pickup pickup,
        Transform transform,
        SerializableItemSpawnpoint spawnpoint,
        bool applyScale)
    {
        pickup.Transform.parent = transform;
        pickup.Position = transform.position;
        pickup.Rotation = transform.rotation;

        if (applyScale && spawnpoint.Scale != Vector3.one)
            pickup.Scale = Vector3.Scale(pickup.Scale, spawnpoint.Scale);

        if (spawnpoint.Weight != -1)
            pickup.Weight = spawnpoint.Weight;

        if (pickup.Rigidbody != null)
            pickup.Rigidbody.isKinematic = !spawnpoint.UseGravity;

        pickup.IsLocked = !spawnpoint.CanBePickedUp;
    }

    private static void RemoveVanillaPickups(MapEditorObject mapObject, SpawnpointBinding? existingBinding = null)
    {
        foreach (ItemPickupBase pickupBase in mapObject.GetComponentsInChildren<ItemPickupBase>())
        {
            ushort serial = pickupBase.Info.Serial;
            if (existingBinding?.ContainsSerial(serial) == true || CItem.TryGet(serial, out _))
                continue;

            try
            {
                pickupBase.DestroySelf();
            }
            catch (Exception ex)
            {
                Log.Warn($"[ItemSpawnpointCItemBridge] Failed to remove vanilla pickup serial={serial}: {ex.Message}");
            }
        }
    }

    private static bool HasVanillaPickups(MapEditorObject mapObject)
    {
        foreach (ItemPickupBase pickupBase in mapObject.GetComponentsInChildren<ItemPickupBase>())
        {
            if (!CItem.TryGet(pickupBase.Info.Serial, out _))
                return true;
        }

        return false;
    }

    private static void OnPickingUpItem(PlayerEvents.PickingUpItemEventArgs ev)
    {
        if (ev?.Pickup == null || ev.Player == null)
            return;

        if (!MultiUsePickups.TryGetValue(ev.Pickup.Serial, out CItemUseState state))
            return;

        if (!ev.IsAllowed || state.RemainingUses <= 1)
        {
            MultiUsePickups.Remove(ev.Pickup.Serial);
            return;
        }

        ev.IsAllowed = false;
        state.RemainingUses--;
        state.CItem.Give(ev.Player, displayMessage: true);
    }

    private static string GetBindingKey(string mapName, string objectId)
        => $"{mapName}:{objectId}";

    private static void RemoveMissingBindings(HashSet<string> seenSpawnpoints)
    {
        foreach (string bindingKey in Bindings.Keys.ToList())
        {
            if (seenSpawnpoints.Contains(bindingKey))
                continue;

            DestroyBinding(bindingKey);
        }
    }

    private static void DestroyBinding(string bindingKey)
    {
        if (!Bindings.TryGetValue(bindingKey, out SpawnpointBinding binding))
            return;

        binding.DestroyPickups();
        Bindings.Remove(bindingKey);
    }

    private static void ClearBindings()
    {
        foreach (SpawnpointBinding binding in Bindings.Values.ToList())
            binding.DestroyPickups();

        Bindings.Clear();
        MultiUsePickups.Clear();
    }

    private static IReadOnlyDictionary<string, string> GetConfiguredCItemKeys(string mapName)
    {
        string path = Path.Combine(ProjectMER.ProjectMER.MapsDir, $"{mapName}.yml");
        if (!File.Exists(path))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        DateTime lastWrite = File.GetLastWriteTimeUtc(path);
        if (MapConfigCache.TryGetValue(mapName, out MapCItemConfigCache cache) && cache.LastWrite == lastWrite)
            return cache.CItemKeys;

        Dictionary<string, string> keys = LoadConfiguredCItemKeys(path);
        MapConfigCache[mapName] = new MapCItemConfigCache(lastWrite, keys);
        return keys;
    }

    private static Dictionary<string, string> LoadConfiguredCItemKeys(string path)
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);

        try
        {
            YamlStream yaml = new();
            using StreamReader reader = File.OpenText(path);
            yaml.Load(reader);

            if (yaml.Documents.Count == 0 ||
                yaml.Documents[0].RootNode is not YamlMappingNode root ||
                !TryGetMapping(root, ItemSpawnpointKeys, out YamlMappingNode itemSpawnpoints))
            {
                return result;
            }

            foreach (KeyValuePair<YamlNode, YamlNode> pair in itemSpawnpoints.Children)
            {
                if (pair.Key is not YamlScalarNode idNode ||
                    string.IsNullOrWhiteSpace(idNode.Value) ||
                    pair.Value is not YamlMappingNode spawnpointNode ||
                    !TryGetScalar(spawnpointNode, CItemKeyFields, out string cItemKey) ||
                    string.IsNullOrWhiteSpace(cItemKey))
                {
                    continue;
                }

                result[idNode.Value!] = cItemKey.Trim();
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"[ItemSpawnpointCItemBridge] Failed to read CItem ItemSpawnpoint config '{path}': {ex.Message}");
        }

        return result;
    }

    private static bool TryGetMapping(YamlMappingNode mapping, IEnumerable<string> names, out YamlMappingNode value)
    {
        value = null!;
        if (!TryGetNode(mapping, names, out YamlNode node) || node is not YamlMappingNode typed)
            return false;

        value = typed;
        return true;
    }

    private static bool TryGetScalar(YamlMappingNode mapping, IEnumerable<string> names, out string value)
    {
        value = string.Empty;
        if (!TryGetNode(mapping, names, out YamlNode node) || node is not YamlScalarNode scalar)
            return false;

        value = scalar.Value ?? string.Empty;
        return true;
    }

    private static bool TryGetNode(YamlMappingNode mapping, IEnumerable<string> names, out YamlNode value)
    {
        foreach (KeyValuePair<YamlNode, YamlNode> pair in mapping.Children)
        {
            if (pair.Key is not YamlScalarNode keyNode || keyNode.Value == null)
                continue;

            if (names.Any(name => keyNode.Value.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                value = pair.Value;
                return true;
            }
        }

        value = null!;
        return false;
    }

    private sealed class SpawnpointBinding
    {
        private readonly int _numberOfUses;
        private readonly uint _numberOfItems;
        private readonly float _weight;
        private readonly bool _useGravity;
        private readonly bool _canBePickedUp;
        private readonly Vector3 _scale;

        public SpawnpointBinding(
            MapEditorObject mapObject,
            string cItemKey,
            SerializableItemSpawnpoint spawnpoint,
            List<Pickup> pickups)
        {
            MapName = mapObject.MapName;
            ObjectId = mapObject.Id;
            CItemKey = cItemKey;
            Transform = mapObject.transform;
            Pickups = pickups;
            _numberOfUses = spawnpoint.NumberOfUses;
            _numberOfItems = spawnpoint.NumberOfItems;
            _weight = spawnpoint.Weight;
            _useGravity = spawnpoint.UseGravity;
            _canBePickedUp = spawnpoint.CanBePickedUp;
            _scale = spawnpoint.Scale;
        }

        public string MapName { get; }

        public string ObjectId { get; }

        public string CItemKey { get; }

        public Transform Transform { get; }

        public List<Pickup> Pickups { get; }

        public bool Matches(string mapName, MapEditorObject mapObject, string cItemKey, SerializableItemSpawnpoint spawnpoint)
            => MapName.Equals(mapName, StringComparison.OrdinalIgnoreCase) &&
               ObjectId.Equals(mapObject.Id, StringComparison.OrdinalIgnoreCase) &&
               CItemKey.Equals(cItemKey, StringComparison.OrdinalIgnoreCase) &&
               _numberOfUses == spawnpoint.NumberOfUses &&
               _numberOfItems == spawnpoint.NumberOfItems &&
               Math.Abs(_weight - spawnpoint.Weight) < 0.001f &&
               _useGravity == spawnpoint.UseGravity &&
               _canBePickedUp == spawnpoint.CanBePickedUp &&
               _scale == spawnpoint.Scale;

        public bool ContainsSerial(ushort serial)
            => Pickups.Any(pickup => pickup != null && pickup.Serial == serial);

        public void SyncPickups(Transform transform, SerializableItemSpawnpoint spawnpoint)
        {
            foreach (Pickup pickup in Pickups.ToList())
            {
                if (pickup?.GameObject == null)
                    continue;

                ApplySpawnpointSettings(pickup, transform, spawnpoint, applyScale: false);
            }
        }

        public void DestroyPickups()
        {
            foreach (Pickup pickup in Pickups.ToList())
            {
                if (pickup == null)
                    continue;

                MultiUsePickups.Remove(pickup.Serial);

                try
                {
                    if (pickup.GameObject != null)
                    {
                        CItem.SerialTracker.ForceUnregister(pickup.Serial);
                        pickup.Destroy();
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn($"[ItemSpawnpointCItemBridge] Failed to destroy CItem pickup serial={pickup.Serial}: {ex.Message}");
                }
            }

            Pickups.Clear();
        }
    }

    private sealed class CItemUseState
    {
        public CItemUseState(CItem cItem, int remainingUses)
        {
            CItem = cItem;
            RemainingUses = remainingUses;
        }

        public CItem CItem { get; }

        public int RemainingUses { get; set; }
    }

    private readonly struct MapCItemConfigCache
    {
        public MapCItemConfigCache(DateTime lastWrite, IReadOnlyDictionary<string, string> cItemKeys)
        {
            LastWrite = lastWrite;
            CItemKeys = cItemKeys;
        }

        public DateTime LastWrite { get; }

        public IReadOnlyDictionary<string, string> CItemKeys { get; }
    }
}
