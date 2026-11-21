using Exiled.API.Enums;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// A server-authored spatial volume declared as a zone and, optionally, a named room.
/// An empty <see cref="Id"/> denotes a zone-only volume.
/// </summary>
public sealed class CustomRoom
{
    public CustomRoom(string id, ZoneType zone, Bounds bounds)
    {
        Id = id ?? string.Empty;
        Zone = zone;
        Bounds = bounds;
    }

    /// <summary>Normalized room id. Empty for a zone-only declaration.</summary>
    public string Id { get; }

    /// <summary>The zone this volume belongs to.</summary>
    public ZoneType Zone { get; }

    /// <summary>World-space axis-aligned bounds of the volume.</summary>
    public Bounds Bounds { get; }

    public Vector3 Position => Bounds.center;

    public bool Contains(Vector3 point) => Bounds.Contains(point);
}
