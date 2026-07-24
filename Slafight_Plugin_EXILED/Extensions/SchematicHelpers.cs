using System;
using Exiled.API.Features;
using MEC;
using ProjectMER.Events.Arguments;
using ProjectMER.Features.Objects;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Extensions;

public static class SchematicHelpers
{
    public static bool TryGetPosition(this SchematicSpawnedEventArgs ev, out Vector3 position)
    {
        position = default;

        if (ev.Schematic == null || ev.Schematic.gameObject == null)
            return false;

        try
        {
            position = ev.Schematic.Position;
            return true;
        }
        catch (Exception e)
        {
            Log.Error(
                $"[SchematicHelpers] Failed to get position for schematic {ev.Schematic.Name}: {e}");
            return false;
        }
    }

    public static void DestroySafe(this SchematicSpawnedEventArgs ev, float delay = 0f)
    {
        if (ev.Schematic == null || ev.Schematic.gameObject == null)
            return;

        if (delay <= 0f)
        {
            ev.Schematic.Destroy();
        }
        else
        {
            Timing.CallDelayed(delay, () =>
            {
                if (ev.Schematic != null && ev.Schematic.gameObject != null)
                    ev.Schematic.Destroy();
            });
        }
    }

    public static void DestroySafe(this SchematicObject schematic, float delay = 0f)
    {
        if (schematic == null || schematic.gameObject == null)
            return;

        if (delay <= 0f)
        {
            schematic.Destroy();
        }
        else
        {
            Timing.CallDelayed(delay, () =>
            {
                if (schematic != null && schematic.gameObject != null)
                    schematic.Destroy();
            });
        }
    }
}