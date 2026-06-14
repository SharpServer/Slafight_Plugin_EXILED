using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Exiled.API.Features;
using LabApi.Events.Handlers;
using MEC;
using ProjectMER.Events.Arguments;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Interface;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Slafight_Plugin_EXILED.CustomMaps.Bridges;

public class SchematicObjectPrefabBridge : SlafightLabApiHandler, IBootstrapHandler
{
    private static SchematicObjectPrefabBridge _instance;
    private static readonly Dictionary<int, MarkerBinding> MarkerBindings = new();
    private static CoroutineHandle _syncCoroutine;
    private static CoroutineHandle _discoveryCoroutine;
    private static CoroutineHandle _scheduledSpawnCoroutine;
    private static int _spawnRefreshGeneration;

    public static void Register() => _instance = LabApiHandlerRegistry.Register(_instance);

    public static void Unregister()
    {
        ClearMarkerPrefabs();
        _spawnRefreshGeneration++;
        if (_scheduledSpawnCoroutine.IsRunning)
            Timing.KillCoroutines(_scheduledSpawnCoroutine);

        if (_discoveryCoroutine.IsRunning)
            Timing.KillCoroutines(_discoveryCoroutine);

        LabApiHandlerRegistry.Unregister(ref _instance);
    }

    protected override void RegisterEvents(EventSubscriptionScope subscriptions)
    {
        subscriptions.Add(() => ServerEvents.WaitingForPlayers += OnWaitingForPlayers, () => ServerEvents.WaitingForPlayers -= OnWaitingForPlayers);
        subscriptions.Add(() => ServerEvents.RoundStarted += OnRoundStarted, () => ServerEvents.RoundStarted -= OnRoundStarted);
        subscriptions.Add(() => ServerEvents.RoundRestarted += OnRoundRestarted, () => ServerEvents.RoundRestarted -= OnRoundRestarted);
        subscriptions.Add(
            () => ProjectMER.Events.Handlers.Schematic.SchematicSpawned += OnSchematicSpawned,
            () => ProjectMER.Events.Handlers.Schematic.SchematicSpawned -= OnSchematicSpawned);
    }

    private static void OnWaitingForPlayers()
    {
        ScheduleSpawnFromMarkers(5.1f);
        EnsureDiscoveryCoroutine();
    }

    private static void OnRoundStarted()
    {
        ScheduleSpawnFromMarkers(2.6f);
        EnsureDiscoveryCoroutine();
    }

    private static void OnRoundRestarted()
    {
        ClearMarkerPrefabs();
    }

    private static void OnSchematicSpawned(SchematicSpawnedEventArgs ev)
    {
        if (!ContainsObjectPrefabMarkers(ev))
            return;

        ScheduleSpawnFromMarkers(0.1f);
    }

    private static bool ContainsObjectPrefabMarkers(SchematicSpawnedEventArgs ev)
    {
        Type markerType = GetMarkerType();
        return markerType != null &&
               ev.Schematic.GetComponentsInChildren(markerType, false).Length > 0;
    }

    private static void ScheduleSpawnFromMarkers(float delay)
    {
        int generation = ++_spawnRefreshGeneration;
        if (_scheduledSpawnCoroutine.IsRunning)
            Timing.KillCoroutines(_scheduledSpawnCoroutine);

        _scheduledSpawnCoroutine = Timing.CallDelayed(delay, () =>
        {
            if (generation != _spawnRefreshGeneration)
                return;

            SpawnFromMarkers();
        });
    }

    private static void SpawnFromMarkers()
    {
        Type markerType = GetMarkerType();
        if (markerType == null)
        {
            Log.Debug("[SchematicObjectPrefabBridge] ProjectMER marker type was not found; schematic ObjectPrefab markers are unavailable.");
            return;
        }

        int spawned = 0;
        HashSet<int> seenMarkers = [];
        foreach (Object marker in Object.FindObjectsByType(markerType, FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!TryReadMarker(marker, out MarkerData data))
                continue;

            if (string.IsNullOrWhiteSpace(data.PrefabType))
                continue;

            int markerId = marker.GetInstanceID();
            seenMarkers.Add(markerId);

            if (TrySyncExistingMarkerPrefab(markerId, data))
                continue;

            if (!TryResolvePrefabType(data.PrefabType, out Type prefabType))
            {
                Log.Warn($"[SchematicObjectPrefabBridge] ObjectPrefab type '{data.PrefabType}' was not found.");
                continue;
            }

            var prefab = (ObjectPrefab)Activator.CreateInstance(prefabType)!;
            prefab.Position = data.Transform.position;
            prefab.Rotation = data.Transform.rotation;
            prefab.Scale = data.Transform.lossyScale;
            prefab.MaxRooms = Math.Max(1, data.MaxRooms);
            prefab.AutoDestroyEnabled = data.AutoDestroyEnabled;
            prefab.AutoDestroyTime = data.AutoDestroyTime;
            prefab.IsSaveable = false;

            if (data.Options.Count > 0)
                prefab.ApplyOptions(data.Options);

            prefab.Create();
            MarkerBindings[markerId] = new MarkerBinding(data.Transform, prefab.ObjectInstanceID, data.PrefabType, data.Options);
            spawned++;
        }

        RemoveMissingMarkerPrefabs(seenMarkers);
        EnsureSyncCoroutine();

        if (spawned > 0)
            Log.Info($"[SchematicObjectPrefabBridge] Spawned {spawned} ObjectPrefabs from schematic markers.");
    }

    private static bool TrySyncExistingMarkerPrefab(int markerId, MarkerData data)
    {
        if (!MarkerBindings.TryGetValue(markerId, out MarkerBinding binding))
            return false;

        ObjectPrefab? prefab = InstanceManager.Get(binding.PrefabId);
        if (prefab == null)
        {
            MarkerBindings.Remove(markerId);
            return false;
        }

        if (!binding.PrefabType.Equals(data.PrefabType, StringComparison.OrdinalIgnoreCase))
        {
            prefab.Destroy();
            MarkerBindings.Remove(markerId);
            return false;
        }

        SyncPrefabTransform(prefab, data.Transform);
        SyncPrefabOptions(markerId, prefab, binding, data.Options);
        return true;
    }

    private static void SyncPrefabOptions(int markerId, ObjectPrefab prefab, MarkerBinding binding, Dictionary<string, string> options)
    {
        if (AreOptionsEqual(binding.Options, options))
            return;

        prefab.ApplyOptions(options);
        prefab.SyncManagedObjects();
        MarkerBindings[markerId] = binding.WithOptions(options);
    }

    private static void RemoveMissingMarkerPrefabs(HashSet<int> seenMarkers)
    {
        foreach (KeyValuePair<int, MarkerBinding> pair in MarkerBindings.ToList())
        {
            if (seenMarkers.Contains(pair.Key))
                continue;

            ObjectPrefab? prefab = InstanceManager.Get(pair.Value.PrefabId);
            prefab?.Destroy();
            MarkerBindings.Remove(pair.Key);
        }
    }

    private static void ClearMarkerPrefabs()
    {
        foreach (MarkerBinding binding in MarkerBindings.Values.ToList())
        {
            ObjectPrefab? prefab = InstanceManager.Get(binding.PrefabId);
            prefab?.Destroy();
        }

        MarkerBindings.Clear();
        if (_syncCoroutine.IsRunning)
            Timing.KillCoroutines(_syncCoroutine);
    }

    private static void EnsureDiscoveryCoroutine()
    {
        if (_discoveryCoroutine.IsRunning)
            return;

        _discoveryCoroutine = Timing.RunCoroutine(DiscoverMarkerPrefabsCoroutine());
    }

    private static IEnumerator<float> DiscoverMarkerPrefabsCoroutine()
    {
        while (true)
        {
            SpawnFromMarkers();
            yield return Timing.WaitForSeconds(1f);
        }
    }

    private static void EnsureSyncCoroutine()
    {
        if (MarkerBindings.Count == 0 || _syncCoroutine.IsRunning)
            return;

        _syncCoroutine = Timing.RunCoroutine(SyncMarkerPrefabsCoroutine());
    }

    private static IEnumerator<float> SyncMarkerPrefabsCoroutine()
    {
        while (MarkerBindings.Count > 0)
        {
            foreach (KeyValuePair<int, MarkerBinding> pair in MarkerBindings.ToList())
            {
                if (pair.Value.Transform == null)
                {
                    ObjectPrefab? missingMarkerPrefab = InstanceManager.Get(pair.Value.PrefabId);
                    missingMarkerPrefab?.Destroy();
                    MarkerBindings.Remove(pair.Key);
                    continue;
                }

                ObjectPrefab? prefab = InstanceManager.Get(pair.Value.PrefabId);
                if (prefab == null)
                {
                    MarkerBindings.Remove(pair.Key);
                    continue;
                }

                SyncPrefabTransform(prefab, pair.Value.Transform);
            }

            yield return Timing.WaitForSeconds(0.05f);
        }
    }

    private static void SyncPrefabTransform(ObjectPrefab prefab, Transform markerTransform)
    {
        prefab.Position = markerTransform.position;
        prefab.Rotation = markerTransform.rotation;
        prefab.Scale = markerTransform.lossyScale;
    }

    private static bool AreOptionsEqual(IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right)
    {
        if (left.Count != right.Count)
            return false;

        foreach (KeyValuePair<string, string> pair in left)
        {
            if (!right.TryGetValue(pair.Key, out string value) || value != pair.Value)
                return false;
        }

        return true;
    }

    private static Type GetMarkerType()
        => Type.GetType("ProjectMER.Features.Objects.SchematicObjectPrefabObject, ProjectMER");

    private static bool TryResolvePrefabType(string input, out Type prefabType)
    {
        prefabType = Type.GetType(input) ??
                     Assembly.GetExecutingAssembly().GetTypes()
                         .FirstOrDefault(t =>
                             !t.IsAbstract &&
                             t.IsSubclassOf(typeof(ObjectPrefab)) &&
                             (t.FullName == input ||
                              t.Name.Equals(input, StringComparison.OrdinalIgnoreCase) ||
                              t.FullName?.EndsWith("." + input, StringComparison.OrdinalIgnoreCase) == true));

        return prefabType != null;
    }

    private static bool TryReadMarker(Object marker, out MarkerData data)
    {
        data = default;

        if (marker is not Component component)
            return false;

        Type type = marker.GetType();
        data = new MarkerData
        {
            Transform = component.transform,
            PrefabType = ReadField<string>(type, marker, "PrefabType") ?? string.Empty,
            MaxRooms = ReadField<int>(type, marker, "MaxRooms"),
            AutoDestroyEnabled = ReadField<bool>(type, marker, "AutoDestroyEnabled"),
            AutoDestroyTime = ReadField<float>(type, marker, "AutoDestroyTime"),
            Options = ReadField<Dictionary<string, string>>(type, marker, "Options") ?? new Dictionary<string, string>(),
        };

        return true;
    }

    private static T? ReadField<T>(Type type, object instance, string name)
    {
        FieldInfo? field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (field == null)
            return default;

        object? value = field.GetValue(instance);
        return value is T typed ? typed : default;
    }

    private struct MarkerData
    {
        public Transform Transform;
        public string PrefabType;
        public int MaxRooms;
        public bool AutoDestroyEnabled;
        public float AutoDestroyTime;
        public Dictionary<string, string> Options;
    }

    private readonly struct MarkerBinding
    {
        public MarkerBinding(Transform transform, string prefabId, string prefabType, Dictionary<string, string> options)
        {
            Transform = transform;
            PrefabId = prefabId;
            PrefabType = prefabType;
            Options = CopyOptions(options);
        }

        public Transform Transform { get; }

        public string PrefabId { get; }

        public string PrefabType { get; }

        public IReadOnlyDictionary<string, string> Options { get; }

        public MarkerBinding WithOptions(Dictionary<string, string> options) => new(Transform, PrefabId, PrefabType, options);

        private static Dictionary<string, string> CopyOptions(Dictionary<string, string> options)
            => options.Count == 0 ? new Dictionary<string, string>() : new Dictionary<string, string>(options);
    }
}
