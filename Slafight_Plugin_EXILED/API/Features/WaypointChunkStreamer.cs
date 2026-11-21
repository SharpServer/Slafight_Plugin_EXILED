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
    public const float DefaultGcDelay = 30f;

    private const float UpdateInterval = 0.1f;
    private const int MinFreeWaypointSlots = 16;
    private const float PressureGcDelayFloor = 5f;

    private static readonly Dictionary<ChunkKey, ActiveChunk> ActiveChunks = new();
    private static readonly HashSet<ChunkKey> RequiredChunks = [];
    private static readonly HashSet<ChunkKey> CoreChunks = [];
    private static readonly HashSet<byte> ReferencedWaypointIds = [];
    private static readonly List<ChunkKey> RemovalBuffer = [];

    private static CoroutineHandle _streamingCoroutine;
    private static bool _registered;
    private static bool _underPressure;
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
        CollectRequiredChunks(position, RequiredChunks, includePreload: !IsUnderPressure());

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
        CollectReferencedWaypointIds();
        int referencedChunks = 0;
        foreach (ActiveChunk active in ActiveChunks.Values)
        {
            if (IsWaypointReferenced(active.Waypoint.WaypointId))
                referencedChunks++;
        }

        int occupied = CountOccupiedWaypointSlots();
        int others = Mathf.Clamp(occupied - ActiveChunks.Count, 0, occupied);
        int budget = Mathf.Max(0, (byte.MaxValue - 1) - MinFreeWaypointSlots - others);

        return
            $"Waypoint stream: {state}\n" +
            $"Active chunks: {ActiveChunks.Count} ({referencedChunks} currently referenced)\n" +
            $"Waypoint slots: {occupied}/{(byte.MaxValue - 1)} occupied, {ActiveChunks.Count} streamed, {others} other, budget={budget}\n" +
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
        bool pressure = IsUnderPressure();
        float now = Time.realtimeSinceStartup;
        CollectReferencedWaypointIds();

        RequiredChunks.Clear();
        CoreChunks.Clear();
        if (IsConfigured)
        {
            foreach (Player player in Player.List)
            {
                if (!ShouldTrack(player))
                    continue;

                CollectRequiredChunks(player.Position, RequiredChunks, includePreload: !pressure);
                CollectRequiredChunks(player.Position, CoreChunks, includePreload: false);
            }
        }

        foreach (ChunkKey key in RequiredChunks)
        {
            if (ActiveChunks.TryGetValue(key, out ActiveChunk active))
                active.LastNeededAt = now;
        }

        float gc = pressure
            ? Mathf.Max(PressureGcDelayFloor, GcDelay * 0.25f)
            : GcDelay;

        // Eviction runs before spawning so freed waypoint IDs can be reused immediately.
        RemovalBuffer.Clear();
        foreach (KeyValuePair<ChunkKey, ActiveChunk> pair in ActiveChunks)
        {
            ActiveChunk active = pair.Value;
            if (RequiredChunks.Contains(pair.Key))
                continue;

            bool isReferenced = IsWaypointReferenced(active.Waypoint.WaypointId);
            if (isReferenced)
            {
                active.LastReferencedAt = now;
                active.WasEverReferenced = true;
            }

            // A client can still have movement packets in flight after it switches to a
            // different waypoint. Delay ID reuse from both the last coverage request and
            // the last observed reference; reusing an ID too early decodes those packets
            // relative to a completely different chunk and causes position corrections.
            // Only chunks no client has ever referenced may use the shortened pressure
            // delay; once a waypoint has carried client packets it keeps the full GcDelay.
            float lastUse = Mathf.Max(active.LastNeededAt, active.LastReferencedAt);
            float effectiveGc = active.WasEverReferenced ? GcDelay : gc;
            if (now - lastUse < effectiveGc)
                continue;

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

        // Core (single-chunk) coverage first, then the best-effort preload ring.
        foreach (ChunkKey key in CoreChunks)
            SpawnChunkIfMissing(key, now);
        foreach (ChunkKey key in RequiredChunks)
            SpawnChunkIfMissing(key, now);
    }

    private static void SpawnChunkIfMissing(ChunkKey key, float now)
    {
        if (ActiveChunks.ContainsKey(key))
            return;

        TrySpawnChunk(key, now);
    }

    private static bool TrySpawnChunk(ChunkKey key, float now)
    {
        if (!HasWaypointCapacity())
        {
            if (now - _lastCapacityWarning >= 5f)
            {
                int occupied = CountOccupiedWaypointSlots();
                int cap = byte.MaxValue - 1 - MinFreeWaypointSlots;
                Log.Warn($"[WaypointChunkStreamer] Waypoint ID capacity reached ({occupied}/{cap} occupied, {ActiveChunks.Count} streamed chunks); new chunks cannot be spawned.");
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

    private static void CollectRequiredChunks(Vector3 position, HashSet<ChunkKey> output, bool includePreload)
    {
        if (!IsFinite(position))
            return;

        Vector3 low = includePreload ? position - Vector3.one * PreloadDistance : position;
        Vector3 high = includePreload ? position + Vector3.one * PreloadDistance : position;
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
               player.IsSafePlayer() &&
               player.ReferenceHub?.roleManager?.CurrentRole is IFpcRole &&
               IsFinite(player.Position);
    }

    private static bool HasReferencedChunks()
    {
        CollectReferencedWaypointIds();
        foreach (ActiveChunk active in ActiveChunks.Values)
        {
            if (IsWaypointReferenced(active.Waypoint.WaypointId))
                return true;
        }

        return false;
    }

    private static void CollectReferencedWaypointIds()
    {
        ReferencedWaypointIds.Clear();
        foreach (Player player in Player.List)
        {
            if (player?.ReferenceHub?.roleManager?.CurrentRole is not IFpcRole fpcRole ||
                !fpcRole.FpcModule.ModuleReady)
                continue;

            ReferencedWaypointIds.Add(fpcRole.FpcModule.RelativePosition.WaypointId);
            ReferencedWaypointIds.Add(fpcRole.FpcModule.Motor.ReceivedPosition.WaypointId);
        }
    }

    private static bool IsWaypointReferenced(byte waypointId)
        => ReferencedWaypointIds.Contains(waypointId);

    private static int CountOccupiedWaypointSlots()
    {
        int occupied = 0;
        for (int i = 1; i < byte.MaxValue; i++)
        {
            if (WaypointBase.SetWaypoints[i])
                occupied++;
        }

        return occupied;
    }

    private static bool HasWaypointCapacity()
        => HasWaypointCapacity(CountOccupiedWaypointSlots());

    private static bool HasWaypointCapacity(int occupied)
        => occupied < byte.MaxValue - 1 - MinFreeWaypointSlots;

    private static bool IsUnderPressure()
    {
        int occupied = CountOccupiedWaypointSlots();
        int others = Mathf.Clamp(occupied - ActiveChunks.Count, 0, occupied);
        int budget = Mathf.Max(0, (byte.MaxValue - 1) - MinFreeWaypointSlots - others);

        if (!HasWaypointCapacity(occupied))
        {
            _underPressure = true;
            return true;
        }

        // Hysteresis: once we drop preloading, keep it off until there is enough
        // headroom to rebuild the ring. Otherwise eviction and respawn oscillate
        // every tick and churn network objects for no benefit.
        if (_underPressure)
        {
            if (ActiveChunks.Count < budget - 24)
                _underPressure = false;
        }
        else if (ActiveChunks.Count >= budget - 4)
        {
            _underPressure = true;
        }

        return _underPressure;
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
        CoreChunks.Clear();
        RemovalBuffer.Clear();
        ReferencedWaypointIds.Clear();
        _underPressure = false;
        _lastCapacityWarning = 0f;
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
            LastReferencedAt = lastNeededAt;
        }

        public Waypoint Waypoint { get; }
        public float LastNeededAt { get; set; }
        public float LastReferencedAt { get; set; }
        public bool WasEverReferenced { get; set; }
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

/// <summary>
/// <see cref="WaypointChunkStreamer"/> の起動と停止を受け持ちます。
/// </summary>
public sealed class WaypointChunkStreamerLifecycle : Slafight_Plugin_EXILED.API.Core.Features.EventHandlerBase
{
    /// <inheritdoc />
    public override void RegisterEvents() => WaypointChunkStreamer.RegisterEvents();

    /// <inheritdoc />
    public override void UnregisterEvents() => WaypointChunkStreamer.UnregisterEvents();
}
