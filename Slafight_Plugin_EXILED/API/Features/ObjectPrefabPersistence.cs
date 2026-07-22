using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Exiled.API.Features;
using MEC;
using Newtonsoft.Json;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>Loads and spawns saved ObjectPrefab maps using descriptor keys.</summary>
public static class ObjectPrefabLoader
{
    /// <summary>Last normalized map name loaded by this process.</summary>
    public static string? LastLoadedMapName { get; private set; }

    /// <summary>Per-frame prefab creation budget for staggered loading.</summary>
    public const float DefaultLoadFrameBudgetMs = 6f;

    private static CoroutineHandle _staggeredLoadHandle;

    public static int LoadMap(string mapName)
    {
        KillStaggeredLoad();
        if (!ObjectPrefabConfig.TryNormalizeMapName(mapName, out string normalizedMapName))
        {
            Log.Warn($"[ObjectPrefabLoader] Invalid map name '{mapName}'.");
            return 0;
        }

        LastLoadedMapName = normalizedMapName;
        ObjectPrefabConfig cfg = ObjectPrefabConfig.Load(normalizedMapName);
        ClearSaveablePrefabs();
        int totalSpawned = 0;

        foreach (PrefabSaveData data in cfg.Prefabs)
        {
            if (!TryGetSpawnTargets(data, out ObjectPrefabDescriptor descriptor, out List<Room> rooms, out int maxRooms))
                continue;

            for (int i = 0; i < maxRooms; i++)
            {
                if (TrySpawnPrefab(descriptor, data, rooms[i]))
                    totalSpawned++;
            }
        }

        Log.Info($"[ObjectPrefabLoader] Loaded map '{normalizedMapName}' ({totalSpawned} prefabs spawned).");
        return totalSpawned;
    }

    public static CoroutineHandle LoadMapStaggered(string mapName, float frameBudgetMs = DefaultLoadFrameBudgetMs)
    {
        KillStaggeredLoad();
        _staggeredLoadHandle = Timing.RunCoroutine(LoadMapCoroutine(mapName, frameBudgetMs));
        return _staggeredLoadHandle;
    }

    private static IEnumerator<float> LoadMapCoroutine(string mapName, float frameBudgetMs)
    {
        if (!ObjectPrefabConfig.TryNormalizeMapName(mapName, out string normalizedMapName))
        {
            Log.Warn($"[ObjectPrefabLoader] Invalid map name '{mapName}'.");
            yield break;
        }

        LastLoadedMapName = normalizedMapName;
        ObjectPrefabConfig cfg = ObjectPrefabConfig.Load(normalizedMapName);
        ClearSaveablePrefabs();
        int totalSpawned = 0;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        foreach (PrefabSaveData data in cfg.Prefabs)
        {
            if (!TryGetSpawnTargets(data, out ObjectPrefabDescriptor descriptor, out List<Room> rooms, out int maxRooms))
                continue;

            for (int i = 0; i < maxRooms; i++)
            {
                var prefabStopwatch = System.Diagnostics.Stopwatch.StartNew();
                bool spawned = TrySpawnPrefab(descriptor, data, rooms[i]);
                prefabStopwatch.Stop();
                if (spawned)
                    totalSpawned++;

                if (prefabStopwatch.Elapsed.TotalMilliseconds >= 20d)
                    Log.Warn($"[ObjectPrefabLoader] TrySpawnPrefab slow: key='{descriptor.Key}' took={prefabStopwatch.Elapsed.TotalMilliseconds:F1}ms");

                if (stopwatch.Elapsed.TotalMilliseconds >= frameBudgetMs)
                {
                    yield return Timing.WaitForOneFrame;
                    stopwatch.Restart();
                }
            }
        }

        Log.Info($"[ObjectPrefabLoader] Loaded map '{normalizedMapName}' ({totalSpawned} prefabs spawned, staggered).");
    }

    private static void KillStaggeredLoad()
    {
        if (_staggeredLoadHandle.IsRunning)
            Timing.KillCoroutines(_staggeredLoadHandle);
        _staggeredLoadHandle = default;
    }

    private static bool TryGetSpawnTargets(
        PrefabSaveData data,
        out ObjectPrefabDescriptor descriptor,
        out List<Room> rooms,
        out int maxRooms)
    {
        descriptor = null!;
        rooms = null!;
        maxRooms = 0;

        string input = !string.IsNullOrWhiteSpace(data.PrefabKey) ? data.PrefabKey : data.PrefabType;
        bool resolved = ObjectPrefabRegistry.TryResolveExact(input, out descriptor, out string error);
        if (!resolved && !string.IsNullOrWhiteSpace(data.PrefabType) &&
            !string.Equals(input, data.PrefabType, StringComparison.OrdinalIgnoreCase))
        {
            resolved = ObjectPrefabRegistry.TryResolveExact(data.PrefabType, out descriptor, out error);
            input = data.PrefabType;
        }

        if (!resolved)
        {
            Log.Warn($"[ObjectPrefabLoader] {error}");
            return false;
        }

        List<Room> roomsOfType = Room.List.Where(room => room.Type == data.RoomType).ToList();
        if (roomsOfType.Count == 0)
        {
            Log.Warn($"[ObjectPrefabLoader] No rooms of type {data.RoomType} found for prefab '{input}'.");
            return false;
        }

        int maxRoomsFromData = data.MaxRooms <= 0 ? roomsOfType.Count : data.MaxRooms;
        maxRooms = Mathf.Min(maxRoomsFromData, roomsOfType.Count);
        rooms = roomsOfType.OrderBy(_ => UnityEngine.Random.value).ToList();
        return true;
    }

    private static bool TrySpawnPrefab(ObjectPrefabDescriptor descriptor, PrefabSaveData data, Room room)
    {
        try
        {
            Quaternion roomRot = room.Rotation;
            Vector3 worldPos = room.Position + roomRot * data.LocalPosition;
            Quaternion worldRot = roomRot * Quaternion.Euler(data.LocalRotationEuler);

            if (!ObjectPrefabRegistry.TryCreate(descriptor, out ObjectPrefab prefab, out string error))
            {
                Log.Error($"[ObjectPrefabLoader] {error}");
                return false;
            }

            prefab.Position = worldPos;
            prefab.Rotation = worldRot;
            prefab.Scale = data.Scale;
            prefab.AutoDestroyEnabled = data.AutoDestroyEnabled;
            prefab.AutoDestroyTime = data.AutoDestroyTime;
            prefab.MaxRooms = data.MaxRooms <= 0 ? 1 : data.MaxRooms;
            prefab.Tag = data.Tag ?? string.Empty;

            if (string.IsNullOrWhiteSpace(prefab.Tag) && data.Options != null &&
                data.Options.TryGetValue("Tag", out string legacyTag))
                prefab.Tag = legacyTag;

            if (data.Options != null && data.Options.Count > 0)
                prefab.ApplyOptions(data.Options);

            prefab.Create();
            return true;
        }
        catch (Exception exception)
        {
            Log.Error($"[ObjectPrefabLoader] Failed to spawn prefab '{descriptor.Key}': {exception}");
            return false;
        }
    }

    private static void ClearSaveablePrefabs()
    {
        foreach (ObjectPrefab prefab in ObjectPrefabInstances.GetAllSnapshot().Where(prefab => prefab.IsSaveable).ToArray())
            prefab.Destroy();
    }
}

/// <summary>Newtonsoft-backed ObjectPrefab map configuration.</summary>
public sealed class ObjectPrefabConfig
{
    public List<PrefabSaveData> Prefabs { get; set; } = [];

    public static string DirectoryPath => Path.Combine(Paths.Configs, "Slafight_Plugin_Exiled", "Maps");

    public static string GetFilePath(string mapName)
    {
        if (!TryNormalizeMapName(mapName, out string normalized))
            throw new ArgumentException($"Invalid ObjectPrefab map name '{mapName}'.", nameof(mapName));
        return Path.Combine(DirectoryPath, normalized + ".json");
    }

    public static bool TryNormalizeMapName(string mapName, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(mapName))
            return false;

        string candidate = mapName.Trim();
        if (candidate == "." || candidate == ".." || candidate.IndexOf('/') >= 0 || candidate.IndexOf('\\') >= 0)
            return false;

        if (candidate.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return false;

        normalized = candidate;
        return true;
    }

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Formatting = Formatting.Indented,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        TypeNameHandling = TypeNameHandling.None,
    };

    public static ObjectPrefabConfig Load(string mapName)
    {
        try
        {
            string path = GetFilePath(mapName);
            Directory.CreateDirectory(DirectoryPath);
            if (!File.Exists(path))
                return new ObjectPrefabConfig();

            return JsonConvert.DeserializeObject<ObjectPrefabConfig>(File.ReadAllText(path), JsonSettings)
                   ?? new ObjectPrefabConfig();
        }
        catch (Exception exception)
        {
            Log.Error($"[ObjectPrefabConfig] Load({mapName}) failed: {exception}");
            return new ObjectPrefabConfig();
        }
    }

    public void Save(string mapName)
    {
        try
        {
            string path = GetFilePath(mapName);
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(path, JsonConvert.SerializeObject(this, JsonSettings));
        }
        catch (Exception exception)
        {
            Log.Error($"[ObjectPrefabConfig] Save({mapName}) failed: {exception}");
        }
    }
}
