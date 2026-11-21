using HarmonyLib;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;
using FacilityZone = MapGeneration.FacilityZone;

namespace Slafight_Plugin_EXILED.Patches;

/// <summary>
/// Last-known zone is what vanilla light containment decontamination checks.
/// </summary>
[HarmonyPatch(typeof(global::MapGeneration.RoomUtils), nameof(global::MapGeneration.RoomUtils.GetLastKnownZone))]
internal static class CustomRoomLastKnownZonePatch
{
    [HarmonyPostfix]
    private static void Postfix(ReferenceHub hub, ref FacilityZone __result)
    {
        if (CustomRooms.Count == 0 || hub == null || hub.transform == null)
            return;

        FacilityZone? zone = CustomRooms.GetFacilityZoneOverride(hub.transform.position);
        if (zone.HasValue && zone.Value != FacilityZone.None)
            __result = zone.Value;
    }
}

/// <summary>
/// Current zone is used by audible / announcement checks.
/// </summary>
[HarmonyPatch(typeof(global::MapGeneration.RoomUtils), nameof(global::MapGeneration.RoomUtils.GetCurrentZone))]
internal static class CustomRoomCurrentZonePatch
{
    [HarmonyPostfix]
    private static void Postfix(ReferenceHub hub, ref FacilityZone __result)
    {
        if (CustomRooms.Count == 0 || hub == null || hub.transform == null)
            return;

        FacilityZone? zone = CustomRooms.GetFacilityZoneOverride(hub.transform.position);
        if (zone.HasValue && zone.Value != FacilityZone.None)
            __result = zone.Value;
    }
}

/// <summary>
/// Raw position-based zone query, patched for completeness.
/// </summary>
[HarmonyPatch(typeof(global::MapGeneration.RoomUtils), nameof(global::MapGeneration.RoomUtils.GetZone))]
internal static class CustomRoomZoneAtPositionPatch
{
    [HarmonyPostfix]
    private static void Postfix(Vector3 worldPos, ref FacilityZone __result)
    {
        if (CustomRooms.Count == 0)
            return;

        FacilityZone? zone = CustomRooms.GetFacilityZoneOverride(worldPos);
        if (zone.HasValue && zone.Value != FacilityZone.None)
            __result = zone.Value;
    }
}
