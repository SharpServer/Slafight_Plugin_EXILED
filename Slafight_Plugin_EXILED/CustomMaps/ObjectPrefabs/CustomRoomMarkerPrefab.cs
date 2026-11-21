using System;
using Exiled.API.Enums;
using Exiled.API.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Interface;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

/// <summary>
/// Declares the marker's AABB volume as a zone / named room. The marker's authored
/// options (Zone, RoomId, Size) fully describe the declaration.
/// </summary>
public sealed class CustomRoomMarkerPrefab : ObjectPrefab, IObjectPrefabMarkerReceiver
{
    private SchematicObjectPrefabObject? marker;
    private CustomRoom? room;

    /// <summary>Zone alias (LCZ, HCZ, EZ, Surface, Pocket, Other) or a full enum name.</summary>
    public string Zone { get; set; } = "LCZ";

    /// <summary>Empty means a zone-only volume; otherwise this is the queryable room id.</summary>
    public string RoomId { get; set; } = string.Empty;

    /// <summary>Explicit volume size. Zero uses the marker's world scale instead.</summary>
    public Vector3 Size { get; set; }

    // Registration must be live as soon as the marker is created.
    protected override float SetupDelay => 0f;

    public void BindMarker(SchematicObjectPrefabObject source) => marker = source;

    protected override void OnCreate() => RegisterRoom();

    protected override void OnDestroy() => UnregisterRoom();

    protected override void OnRoundRestarting() => UnregisterRoom();

    protected override void OnRoundStarted()
    {
        // A prefab reused across rounds may have lost its registration at the boundary.
        if (room == null)
            RegisterRoom();
    }

    private void RegisterRoom()
    {
        if (room != null)
            return;

        if (!ZoneTypeConverter.TryParseZone(Zone, out ZoneType zone))
        {
            Log.Warn($"[CustomRoomMarkerPrefab] Cannot parse zone '{Zone}' on marker '{MarkerName}'; no volume was declared.");
            return;
        }

        Vector3 rawSize = Size.sqrMagnitude > 0f ? Size : Scale;
        Vector3 size = new(Mathf.Abs(rawSize.x), Mathf.Abs(rawSize.y), Mathf.Abs(rawSize.z));
        Vector3 half = size * 0.5f;

        // Rotate the three local half-axes and sum their absolute components:
        // this yields the tight world-space AABB for any rotation (including mirrored scale).
        Vector3 axisX = Rotation * new Vector3(half.x, 0f, 0f);
        Vector3 axisY = Rotation * new Vector3(0f, half.y, 0f);
        Vector3 axisZ = Rotation * new Vector3(0f, 0f, half.z);
        Vector3 extents = new(
            Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
            Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
            Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));

        string id = (RoomId ?? string.Empty).Trim();
        // Bounds takes the FULL size, while extents are half-sizes.
        CustomRoom created = new(id, zone, new Bounds(Position, extents * 2f));
        try
        {
            CustomRooms.Register(created);
            room = created;
        }
        catch (Exception e)
        {
            Log.Warn($"[CustomRoomMarkerPrefab] Failed to register custom room '{id}' on marker '{MarkerName}': {e}");
        }
    }

    private void UnregisterRoom()
    {
        CustomRoom? current = room;
        room = null;
        if (current == null)
            return;

        try
        {
            CustomRooms.Unregister(current);
        }
        catch (Exception e)
        {
            Log.Warn($"[CustomRoomMarkerPrefab] Failed to unregister custom room '{current.Id}' on marker '{MarkerName}': {e}");
        }
    }

    private string MarkerName => marker == null ? "unknown" : marker.name;
}
