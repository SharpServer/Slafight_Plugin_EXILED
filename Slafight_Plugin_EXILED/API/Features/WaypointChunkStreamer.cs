using System;
using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.API.Features.Toys;
using MEC;
using PlayerRoles.FirstPersonControl;
using RelativePositioning;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using ServerHandlers = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.API.Features;

public static class WaypointChunkStreamer
{
    public const float DefaultChunkSize = 192f;
    public const float DefaultPreloadDistance = 32f;
    public const float DefaultGcDelay = 8f;

    private const float UpdateInterval = 0.25f;
    private const int ReservedWaypointSlots = 8;

    private static readonly Dictionary<ChunkKey, ActiveChunk> ActiveChunks = new();
    private static readonly HashSet<ChunkKey> RequiredChunks = [];
    private static readonly List<ChunkKey> RemovalBuffer = [];

    private static CoroutineHandle _streamingCoroutine;
    private static bool _registered;
    private static float _lastCapacityWarning;

    public static bool IsConfigured { get; private set; }

    public static int ActiveChunkCount => ActiveChunks.Count;
    public static float ChunkSize { get; private set; }

    public static float PreloadDistance { get; private set; }

    public static float GcDelay { get; private set; }

    public static void RegisterEvents()
    {
        if (_registered)
            return;

        ServerHandlers.WaitingForPlayers += StartDefault;
        ServerHandlers.RestartingRound += ClearImmediately;
        _registered = true;
    }

    public static void UnregisterEvents()
    {
        if (!_registered)
            return;

        ServerHandlers.WaitingForPlayers -= StartDefault;
        ServerHandlers.RestartingRound -= ClearImmediately;
        ClearImmediately();
        _registered = false;
    }

    public static bool TryConfigure(
        float chunkSize,
        float preloadDistance,
        float gcDelay,
        out string error)
    {
        error = string.Empty;

        if (chunkSize < 32f || chunkSize > 192f)
        {
            error = "Chunk size must be between 32 and 192 metres.";
            return false;
        }

        if (preloadDistance < 4f || preloadDistance > chunkSize * 0.45f)
        {
            error = $"Preload distance must be between 4 and {chunkSize * 0.45f:0.##} metres.";
            return false;
        }

        if (gcDelay < 2f || gcDelay > 120f)
        {
            error = "GC delay must be between 2 and 120 seconds.";
            return false;
        }

        if (HasReferencedChunks())
        {
            error = "Existing streamed waypoints are still referenced by players. Stop streaming and leave the area before reconfiguring.";
            return false;
        }

        ClearImmediately();

        ChunkSize = chunkSize;
        PreloadDistance = preloadDistance;
        GcDelay = gcDelay;
        IsConfigured = true;
        EnsureCoroutine();

        foreach (Player player in Player.List)
        {
            if (ShouldTrack(player))
                EnsureCoverage(player.Position);
        }

        return true;
    }

    private static void StartDefault()
    {
        ClearImmediately();
        if (!TryConfigure(
                DefaultChunkSize,
                DefaultPreloadDistance,
                DefaultGcDelay,
                out string error))
        {
            Log.Error($"[WaypointChunkStreamer] Automatic startup failed: {error}");
        }
    }

    public static void Stop()
    {
        IsConfigured = false;
        EnsureCoroutine();
    }

    public static int EnsureCoverage(Vector3 position)
    {
        if (!IsConfigured || !IsFinite(position))
            return 0;

        RequiredChunks.Clear();
        CollectRequiredChunks(position, RequiredChunks);

        int created = 0;
        float now = Time.realtimeSinceStartup;
        foreach (ChunkKey key in RequiredChunks)
        {
            if (ActiveChunks.TryGetValue(key, out ActiveChunk active))
            {
                active.LastNeededAt = now;
                continue;
            }

            if (TrySpawnChunk(key, now))
                created++;
        }

        return created;
    }

    public static string GetStatus()
    {
        string state = IsConfigured ? "streaming" : ActiveChunks.Count > 0 ? "draining" : "stopped";
        return
            $"Waypoint stream: {state}\n" +
            $"Active chunks: {ActiveChunks.Count}\n" +
            "Bounds: global\n" +
            $"Chunk={ChunkSize:0.##}m, preload={PreloadDistance:0.##}m, GC={GcDelay:0.##}s";
    }

    private static IEnumerator<float> StreamingLoop()
    {
        while (IsConfigured || ActiveChunks.Count > 0)
        {
            UpdateStreaming();
            yield return Timing.WaitForSeconds(UpdateInterval);
        }

        _streamingCoroutine = default;
    }

    private static void UpdateStreaming()
    {
        RequiredChunks.Clear();

        if (IsConfigured)
        {
            foreach (Player player in Player.List)
            {
                if (!ShouldTrack(player))
                    continue;

                CollectRequiredChunks(player.Position, RequiredChunks);
            }
        }

        float now = Time.realtimeSinceStartup;
        foreach (ChunkKey key in RequiredChunks)
        {
            if (ActiveChunks.TryGetValue(key, out ActiveChunk active))
            {
                active.LastNeededAt = now;
                continue;
            }

            TrySpawnChunk(key, now);
        }

        RemovalBuffer.Clear();
        foreach (KeyValuePair<ChunkKey, ActiveChunk> pair in ActiveChunks)
        {
            ActiveChunk active = pair.Value;
            if (RequiredChunks.Contains(pair.Key) ||
                now - active.LastNeededAt < GcDelay ||
                IsWaypointReferenced(active.Waypoint.WaypointId))
            {
                continue;
            }

            RemovalBuffer.Add(pair.Key);
        }

        foreach (ChunkKey key in RemovalBuffer)
        {
            if (!ActiveChunks.TryGetValue(key, out ActiveChunk active))
                continue;

            try
            {
                active.Waypoint.Destroy();
            }
            catch (Exception exception)
            {
                Log.Warn($"[WaypointChunkStreamer] Failed to destroy chunk {key}: {exception.Message}");
            }

            ActiveChunks.Remove(key);
        }
    }

    private static bool TrySpawnChunk(ChunkKey key, float now)
    {
        if (!HasWaypointCapacity())
        {
            if (now - _lastCapacityWarning >= 5f)
            {
                Log.Warn("[WaypointChunkStreamer] Waypoint ID capacity reached; new chunks cannot be spawned.");
                _lastCapacityWarning = now;
            }

            return false;
        }

        Vector3 center = GetChunkCenter(key);
        float boundsSize = Mathf.Min(255.9961f, ChunkSize + PreloadDistance * 2f);
        Waypoint waypoint = null;
        try
        {
            waypoint = Waypoint.Create(
                parent: null,
                position: center,
                rotation: Quaternion.identity,
                scale: Vector3.one * boundsSize,
                priority: 1f,
                visualizeBounds: false,
                spawn: false);

            waypoint.IsStatic = true;
            waypoint.MovementSmoothing = 0;
            waypoint.Spawn();
            ActiveChunks.Add(key, new ActiveChunk(waypoint, now));
            return true;
        }
        catch (Exception exception)
        {
            Log.Error($"[WaypointChunkStreamer] Failed to spawn chunk {key}: {exception}");
            try
            {
                waypoint?.Destroy();
            }
            catch
            {
                // Best-effort cleanup after a failed spawn.
            }

            return false;
        }
    }

    private static void CollectRequiredChunks(Vector3 position, HashSet<ChunkKey> output)
    {
        if (!IsFinite(position))
            return;

        Vector3 low = position - Vector3.one * PreloadDistance;
        Vector3 high = position + Vector3.one * PreloadDistance;
        Vector3Int first = GetChunkIndex(low);
        Vector3Int last = GetChunkIndex(high);

        for (int x = first.x; x <= last.x; x++)
        for (int y = first.y; y <= last.y; y++)
        for (int z = first.z; z <= last.z; z++)
            output.Add(new ChunkKey(x, y, z));
    }

    private static Vector3Int GetChunkIndex(Vector3 position)
    {
        return new Vector3Int(
            GetChunkIndex(position.x),
            GetChunkIndex(position.y),
            GetChunkIndex(position.z));
    }

    private static int GetChunkIndex(float coordinate)
        => Mathf.FloorToInt(coordinate / ChunkSize);

    private static Vector3 GetChunkCenter(ChunkKey key)
    {
        return new Vector3(
            GetChunkCenter(key.X),
            GetChunkCenter(key.Y),
            GetChunkCenter(key.Z));
    }

    private static float GetChunkCenter(int index)
        => (index + 0.5f) * ChunkSize;

    private static bool ShouldTrack(Player player)
    {
        return player != null &&
               player.IsConnected &&
               player.IsNotHost() &&
               player.ReferenceHub?.roleManager?.CurrentRole is IFpcRole &&
               IsFinite(player.Position);
    }

    private static bool HasReferencedChunks()
    {
        foreach (ActiveChunk active in ActiveChunks.Values)
        {
            if (IsWaypointReferenced(active.Waypoint.WaypointId))
                return true;
        }

        return false;
    }

    private static bool IsWaypointReferenced(byte waypointId)
    {
        foreach (Player player in Player.List)
        {
            if (player?.ReferenceHub?.roleManager?.CurrentRole is not IFpcRole fpcRole ||
                !fpcRole.FpcModule.ModuleReady)
            {
                continue;
            }

            if (fpcRole.FpcModule.RelativePosition.WaypointId == waypointId ||
                fpcRole.FpcModule.Motor.ReceivedPosition.WaypointId == waypointId)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasWaypointCapacity()
    {
        int occupied = 0;
        for (int i = 1; i < byte.MaxValue; i++)
        {
            if (WaypointBase.SetWaypoints[i])
                occupied++;
        }

        return occupied < byte.MaxValue - 1 - ReservedWaypointSlots;
    }

    private static void EnsureCoroutine()
    {
        if (!_streamingCoroutine.IsRunning && (IsConfigured || ActiveChunks.Count > 0))
            _streamingCoroutine = Timing.RunCoroutine(StreamingLoop());
    }

    private static void ClearImmediately()
    {
        IsConfigured = false;

        if (_streamingCoroutine.IsRunning)
            Timing.KillCoroutines(_streamingCoroutine);

        _streamingCoroutine = default;
        foreach (ActiveChunk active in ActiveChunks.Values)
        {
            try
            {
                active.Waypoint.Destroy();
            }
            catch
            {
                // Round teardown can destroy network objects before plugin cleanup runs.
            }
        }

        ActiveChunks.Clear();
        RequiredChunks.Clear();
        RemovalBuffer.Clear();
    }

    private static bool IsFinite(Vector3 value)
        => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

    private static bool IsFinite(float value)
        => !float.IsNaN(value) && !float.IsInfinity(value);

    private sealed class ActiveChunk
    {
        public ActiveChunk(Waypoint waypoint, float lastNeededAt)
        {
            Waypoint = waypoint;
            LastNeededAt = lastNeededAt;
        }

        public Waypoint Waypoint { get; }
        public float LastNeededAt { get; set; }
    }

    private readonly struct ChunkKey : IEquatable<ChunkKey>
    {
        public ChunkKey(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public int X { get; }
        public int Y { get; }
        public int Z { get; }

        public bool Equals(ChunkKey other)
            => X == other.X && Y == other.Y && Z == other.Z;

        public override bool Equals(object obj)
            => obj is ChunkKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = X;
                hashCode = (hashCode * 397) ^ Y;
                hashCode = (hashCode * 397) ^ Z;
                return hashCode;
            }
        }

        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}
