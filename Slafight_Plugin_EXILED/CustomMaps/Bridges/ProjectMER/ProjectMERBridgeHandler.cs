using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Exiled.API.Features;
using InventorySystem.Items.Pickups;
using LabApi.Events.Arguments.ServerEvents;
using MEC;
using ProjectMER.Features;
using ProjectMER.Features.Interfaces;
using ProjectMER.Features.Objects;
using ProjectMER.Features.Serializable;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;
using LabPlayer = LabApi.Features.Wrappers.Player;

namespace Slafight_Plugin_EXILED.CustomMaps.Bridges.ProjectMER;

/// <summary>
/// ProjectMER のマーカー・型情報・ItemSpawnPoint を Slafight API へ接続します。
/// マーカー処理はイベント駆動、型解決は起動時キャッシュです。
/// </summary>
public sealed class ProjectMERBridgeHandler : EventHandlerBase, IObjectPrefabInfoProvider
{
    private const double MarkerBudgetMilliseconds = 3.0;

    private readonly List<PendingMarker> pendingMarkers = [];
    private readonly HashSet<int> pendingMarkerIds = [];
    private readonly Dictionary<int, MarkerBinding> markerBindings = [];
    private readonly Dictionary<string, Type?> customItemTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<ObjectPrefabOptionInfo>> optionDefinitions =
        new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<ObjectPrefabTypeInfo> prefabTypes = [];
    private CoroutineHandle markerWorker;
    private int pendingMarkerIndex;

    public override void RegisterEvents()
    {
        SchematicObjectPrefabObject.Spawned += OnMarkerSpawned;
        SchematicObjectPrefabObject.Destroyed += OnMarkerDestroyed;
    }

    public override void UnregisterEvents()
    {
        SchematicObjectPrefabObject.Spawned -= OnMarkerSpawned;
        SchematicObjectPrefabObject.Destroyed -= OnMarkerDestroyed;
    }

    protected override void OnEnabled()
    {
        BuildPrefabMetadataCache();
        BuildCustomItemTypeCache();
        ObjectPrefabInfoRegistry.Provider = this;
        ItemSpawnpointCustomItemRegistry.Register(TrySpawnCustomItem, TryGiveCustomItem);

        // プラグインの再読込時だけ既存マーカーを一度回収する。通常経路は Spawned イベントのみ。
        foreach (SchematicObjectPrefabObject marker in
                 UnityEngine.Object.FindObjectsByType<SchematicObjectPrefabObject>(FindObjectsSortMode.None))
            QueueMarker(marker);
    }

    protected override void OnDisposed()
    {
        ItemSpawnpointCustomItemRegistry.Unregister(TrySpawnCustomItem, TryGiveCustomItem);
        if (ReferenceEquals(ObjectPrefabInfoRegistry.Provider, this))
            ObjectPrefabInfoRegistry.Provider = null;

        StopMarkerWorker();
        DestroyBindings();
        pendingMarkers.Clear();
        pendingMarkerIds.Clear();
        pendingMarkerIndex = 0;
    }

    public override void OnServerRoundRestarted()
        => ResetRoundState();

    public override void OnServerRoundEnded(RoundEndedEventArgs ev)
        => ResetRoundState();

    private void ResetRoundState()
    {
        StopMarkerWorker();
        ReleaseBindings();
        pendingMarkers.Clear();
        pendingMarkerIds.Clear();
        pendingMarkerIndex = 0;
    }

    public IReadOnlyList<ObjectPrefabTypeInfo> GetPrefabTypes() => prefabTypes;

    public bool TryGetOptionDefinitions(
        string prefabTypeName,
        out IReadOnlyList<ObjectPrefabOptionInfo> definitions,
        out string error)
    {
        definitions = [];
        if (!ObjectPrefabRegistry.TryResolveExact(prefabTypeName, out ObjectPrefabDescriptor descriptor, out error))
            return false;

        if (!optionDefinitions.TryGetValue(descriptor.Key, out definitions!))
        {
            error = $"ObjectPrefab '{descriptor.Key}' has no cached option schema.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void BuildPrefabMetadataCache()
    {
        ObjectPrefabDescriptor[] descriptors = ObjectPrefabRegistry.All.ToArray();
        prefabTypes = descriptors.Select(descriptor => new ObjectPrefabTypeInfo
        {
            Key = descriptor.Key,
            DisplayName = descriptor.DisplayName,
            Aliases = descriptor.Aliases,
        }).ToArray();

        optionDefinitions.Clear();
        foreach (ObjectPrefabDescriptor descriptor in descriptors)
        {
            if (!ObjectPrefabRegistry.TryCreate(descriptor, out ObjectPrefab prefab, out string error))
            {
                Log.Warn($"[ProjectMERBridge] {descriptor.Key} の Option 定義をキャッシュできませんでした: {error}");
                continue;
            }

            optionDefinitions[descriptor.Key] = prefab.GetOptionDefinitions()
                .Select(definition => new ObjectPrefabOptionInfo
                {
                    Name = definition.Name,
                    ValueType = definition.ValueType,
                    DefaultValue = definition.DefaultValue,
                    ConstraintDescription = definition.ConstraintDescription,
                })
                .ToArray();
        }
    }

    private void BuildCustomItemTypeCache()
    {
        customItemTypes.Clear();
        foreach (Type type in TypeParser.FindTypes<CustomItem>())
        {
            AddUnambiguousCustomItemName(type.Name, type);
            if (!string.IsNullOrWhiteSpace(type.FullName))
                customItemTypes[type.FullName!] = type;
        }
    }

    private void AddUnambiguousCustomItemName(string name, Type type)
    {
        if (!customItemTypes.TryGetValue(name, out Type? existing))
            customItemTypes[name] = type;
        else if (existing != type)
            customItemTypes[name] = null;
    }

    private bool TryResolveCustomItem(string name, out Type type)
    {
        type = null!;
        string key = name?.Trim() ?? string.Empty;
        if (key.Length == 0)
            return false;

        if (customItemTypes.TryGetValue(key, out Type? cached))
        {
            type = cached!;
            return cached != null;
        }

        // LegacyName は初回だけ TypeParser へ問い合わせ、失敗も含めて記憶する。
        if (TypeParser.TryParse<CustomItem>(key, out Type? resolved))
        {
            customItemTypes[key] = resolved;
            type = resolved!;
            return true;
        }

        customItemTypes[key] = null;
        return false;
    }

    private bool TrySpawnCustomItem(
        string customItemKey,
        SerializableItemSpawnpoint spawnpoint,
        Vector3 position,
        Quaternion rotation,
        Transform parent,
        out ItemPickupBase? pickup)
    {
        pickup = null;
        if (!TryResolveCustomItem(customItemKey, out Type type))
            return false;

        CustomItem custom = CustomItem.Spawn(type, position, rotation, spawnpoint.Scale);
        pickup = custom?.Pickup?.Base;
        if (pickup == null)
            return false;

        pickup.transform.SetParent(parent, true);
        return true;
    }

    private bool TryGiveCustomItem(string customItemKey, ItemPickupBase pickup, LabPlayer player)
    {
        if (!TryResolveCustomItem(customItemKey, out Type type) || player?.ReferenceHub == null)
            return false;

        Player target = Player.Get(player.ReferenceHub);
        return target != null && CustomItem.Give(type, target) != null;
    }

    private void OnMarkerSpawned(SchematicObjectPrefabObject marker) => QueueMarker(marker);

    private void QueueMarker(SchematicObjectPrefabObject marker)
    {
        if (marker == null)
            return;

        int id = marker.GetInstanceID();
        if (!pendingMarkerIds.Add(id))
            return;

        pendingMarkers.Add(new PendingMarker(id, marker));
        if (!markerWorker.IsRunning)
            markerWorker = Timing.RunCoroutine(ProcessMarkers());
    }

    private IEnumerator<float> ProcessMarkers()
    {
        // SchematicObject の構築中へ再入しないよう、最低 1 フレーム待つ。
        yield return Timing.WaitForOneFrame;

        var stopwatch = new Stopwatch();
        while (pendingMarkerIndex < pendingMarkers.Count)
        {
            stopwatch.Restart();
            do
            {
                PendingMarker pending = pendingMarkers[pendingMarkerIndex++];
                pendingMarkerIds.Remove(pending.Id);
                if (pending.Marker != null && pending.Marker.GetInstanceID() == pending.Id)
                    ApplyMarker(pending.Id, pending.Marker);
            }
            while (pendingMarkerIndex < pendingMarkers.Count &&
                   stopwatch.Elapsed.TotalMilliseconds < MarkerBudgetMilliseconds);

            if (pendingMarkerIndex < pendingMarkers.Count)
                yield return Timing.WaitForOneFrame;
        }

        pendingMarkers.Clear();
        pendingMarkerIndex = 0;
        markerWorker = default;
    }

    private void ApplyMarker(int markerId, SchematicObjectPrefabObject marker)
    {
        if (!ObjectPrefabRegistry.TryResolveExact(marker.PrefabType, out ObjectPrefabDescriptor descriptor, out string error))
        {
            Log.Warn($"[ProjectMERBridge] ObjectPrefab marker '{marker.name}' を解決できません: {error}");
            RemoveBinding(markerId, destroyPrefab: true);
            return;
        }

        if (markerBindings.TryGetValue(markerId, out MarkerBinding existing) &&
            existing.Marker == marker && existing.Prefab.GetType() == descriptor.PrefabType)
        {
            DetachFromMarker(existing.Prefab);
            ConfigurePrefab(existing.Prefab, marker);
            AttachToMarker(existing.Prefab, marker);
            return;
        }

        RemoveBinding(markerId, destroyPrefab: true);
        if (!ObjectPrefabRegistry.TryCreate(descriptor, out ObjectPrefab prefab, out error))
        {
            Log.Warn($"[ProjectMERBridge] ObjectPrefab marker '{marker.name}' の生成に失敗しました: {error}");
            return;
        }

        ConfigurePrefab(prefab, marker);
        try
        {
            prefab.Create();
            AttachToMarker(prefab, marker);
            markerBindings[markerId] = new MarkerBinding(marker, prefab);
        }
        catch (Exception exception)
        {
            prefab.Destroy();
            Log.Error($"[ProjectMERBridge] ObjectPrefab '{descriptor.Key}' の Create に失敗しました: {exception}");
        }
    }

    private static void ConfigurePrefab(ObjectPrefab prefab, SchematicObjectPrefabObject marker)
    {
        prefab.Position = marker.transform.position;
        prefab.Rotation = marker.transform.rotation;
        prefab.Scale = marker.transform.lossyScale;
        prefab.MaxRooms = marker.MaxRooms <= 0 ? 1 : marker.MaxRooms;
        prefab.AutoDestroyEnabled = marker.AutoDestroyEnabled;
        prefab.AutoDestroyTime = marker.AutoDestroyTime;
        prefab.ApplyOptions(marker.Options ?? []);
        prefab.Tag = marker.EffectiveTag;
        prefab.SyncManagedObjects();
    }

    private static void AttachToMarker(ObjectPrefab prefab, SchematicObjectPrefabObject marker)
    {
        if (!prefab.FollowMarkerTransform || marker == null)
            return;

        if (prefab.Schematic != null)
            prefab.Schematic.transform.SetParent(marker.transform, true);

        foreach (InteractableHandle handle in prefab.Interactables)
        {
            if (handle.OwnsToy && handle.Toy?.Transform != null)
                handle.Toy.Transform.SetParent(marker.transform, true);
        }
    }

    private static void DetachFromMarker(ObjectPrefab prefab)
    {
        if (prefab.Schematic != null)
            prefab.Schematic.transform.SetParent(null, true);

        foreach (InteractableHandle handle in prefab.Interactables)
        {
            if (handle.OwnsToy && handle.Toy?.Transform != null)
                handle.Toy.Transform.SetParent(null, true);
        }
    }

    private void OnMarkerDestroyed(SchematicObjectPrefabObject marker)
    {
        if (ReferenceEquals(marker, null))
            return;

        int markerId = marker.GetInstanceID();
        pendingMarkerIds.Remove(markerId);
        RemoveBinding(markerId, destroyPrefab: true);
    }

    private void RemoveBinding(int markerId, bool destroyPrefab)
    {
        if (!markerBindings.TryGetValue(markerId, out MarkerBinding binding))
            return;

        markerBindings.Remove(markerId);
        if (destroyPrefab)
            binding.Prefab.Destroy();
    }

    private void DestroyBindings()
    {
        foreach (MarkerBinding binding in markerBindings.Values.ToArray())
            binding.Prefab.Destroy();
        markerBindings.Clear();
    }

    private void ReleaseBindings()
    {
        foreach (MarkerBinding binding in markerBindings.Values.ToArray())
            binding.Prefab.InvokeRoundRestarting();
        markerBindings.Clear();
    }

    private void StopMarkerWorker()
    {
        if (markerWorker.IsRunning)
            Timing.KillCoroutines(markerWorker);
        markerWorker = default;
    }

    private readonly struct PendingMarker(int id, SchematicObjectPrefabObject marker)
    {
        public int Id { get; } = id;
        public SchematicObjectPrefabObject Marker { get; } = marker;
    }

    private readonly struct MarkerBinding(SchematicObjectPrefabObject marker, ObjectPrefab prefab)
    {
        public SchematicObjectPrefabObject Marker { get; } = marker;
        public ObjectPrefab Prefab { get; } = prefab;
    }
}
