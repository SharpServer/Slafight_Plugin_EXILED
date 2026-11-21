using System;
using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using UnityEngine;
using FacilityZone = MapGeneration.FacilityZone;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// Registry of server-authored zone / room volumes. Snapshots are immutable once published,
/// so the Harmony hot path never takes a lock or allocates.
/// </summary>
public static class CustomRooms
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, CustomRoom> NamedRooms = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<CustomRoom> ZoneOnlyRooms = [];
    private static volatile CustomRoom[] Snapshot = [];

    public static int Count => Snapshot.Length;

    public static IReadOnlyList<CustomRoom> All => Snapshot;

    /// <summary>
    /// Adds a volume. A non-empty id replaces any previous declaration with the same id;
    /// an empty id adds a zone-only volume. Null is ignored.
    /// </summary>
    public static void Register(CustomRoom room)
    {
        if (room == null)
            return;

        lock (Sync)
        {
            if (room.Id.Length == 0)
                ZoneOnlyRooms.Add(room);
            else
                NamedRooms[room.Id] = room;

            RebuildSnapshot();
        }
    }

    /// <summary>Removes a volume by reference. Returns false if it was not registered.</summary>
    public static bool Unregister(CustomRoom room)
    {
        if (room == null)
            return false;

        lock (Sync)
        {
            bool removed = ZoneOnlyRooms.Remove(room);
            if (NamedRooms.TryGetValue(room.Id, out CustomRoom current) && ReferenceEquals(current, room))
            {
                NamedRooms.Remove(room.Id);
                removed = true;
            }

            if (removed)
                RebuildSnapshot();

            return removed;
        }
    }

    /// <summary>Removes a named volume by id. Zone-only volumes are not affected.</summary>
    public static bool Unregister(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        lock (Sync)
        {
            if (!NamedRooms.Remove(id))
                return false;

            RebuildSnapshot();
            return true;
        }
    }

    public static void Clear()
    {
        lock (Sync)
        {
            NamedRooms.Clear();
            ZoneOnlyRooms.Clear();
            RebuildSnapshot();
        }
    }

    public static bool TryGet(string id, out CustomRoom room)
    {
        room = null!;
        if (string.IsNullOrEmpty(id))
            return false;

        CustomRoom[] snapshot = Snapshot;
        for (int i = 0; i < snapshot.Length; i++)
        {
            CustomRoom candidate = snapshot[i];
            if (candidate.Id.Length != 0 && string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                room = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>Finds the first declared volume containing the given world position.</summary>
    public static bool TryGetAt(Vector3 position, out CustomRoom room)
    {
        room = null!;
        CustomRoom[] snapshot = Snapshot;
        for (int i = 0; i < snapshot.Length; i++)
        {
            if (!snapshot[i].Contains(position))
                continue;

            room = snapshot[i];
            return true;
        }

        return false;
    }

    /// <summary>Finds the volume containing the player, if any. No NPC filtering is applied.</summary>
    public static bool TryGet(Player player, out CustomRoom room)
    {
        room = null!;
        if (player == null || player.ReferenceHub == null)
            return false;

        return TryGetAt(player.Position, out room);
    }

    public static bool IsInRoom(Player player, string id)
        => TryGet(player, out CustomRoom room) &&
           room.Id.Length != 0 &&
           string.Equals(room.Id, id, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks the custom declaration first, then falls back to the player's vanilla zone.
    /// </summary>
    public static bool IsInZone(Player player, ZoneType zone)
    {
        ZoneType? custom = GetZoneOverride(player);
        if (custom.HasValue)
            return custom.Value == zone;

        return player != null && player.ReferenceHub != null && player.Zone == zone;
    }

    public static ZoneType? GetZoneOverride(Player player)
    {
        if (player == null || player.ReferenceHub == null)
            return null;

        return TryGetAt(player.Position, out CustomRoom room) ? room.Zone : null;
    }

    public static ZoneType? GetZoneOverride(Vector3 position)
        => TryGetAt(position, out CustomRoom room) ? room.Zone : null;

    public static FacilityZone? GetFacilityZoneOverride(Player player)
    {
        if (player == null || player.ReferenceHub == null)
            return null;

        return GetFacilityZoneOverride(player.Position);
    }

    public static FacilityZone? GetFacilityZoneOverride(Vector3 position)
        => TryGetAt(position, out CustomRoom room)
            ? ZoneTypeConverter.ToFacilityZone(room.Zone)
            : null;

    public static string GetStatus()
    {
        string state = Snapshot.Length == 0 ? "empty" : "active";
        return
            $"Custom rooms: {state}\n" +
            $"Volumes: {Snapshot.Length} ({NamedRooms.Count} named, {ZoneOnlyRooms.Count} zone-only)";
    }

    private static void RebuildSnapshot()
    {
        var rooms = new CustomRoom[NamedRooms.Count + ZoneOnlyRooms.Count];
        int index = 0;
        foreach (CustomRoom room in NamedRooms.Values)
            rooms[index++] = room;
        foreach (CustomRoom room in ZoneOnlyRooms)
            rooms[index++] = room;

        Snapshot = rooms;
    }
}
