using HarmonyLib;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.Handlers;
using InventorySystem.Items.Pickups;
using InventorySystem.Searching;
using Slafight_Plugin_EXILED.API.Core.Features;
using Log = Exiled.API.Features.Log;

namespace Slafight_Plugin_EXILED.Patches;

[HarmonyPatch(typeof(Player), nameof(Player.OnSearchPickupRequest))]
internal static class PickupSearchEventDiagnosticPatch
{
    [HarmonyPostfix]
    private static void Postfix(SearchingPickupEventArgs ev)
    {
        var pickup = ev.Pickup?.Base;
        if (pickup == null || pickup.Info.ItemId != ItemType.Painkillers)
            return;

        Log.Warn($"[PickupDiag] SearchingPickup final serial={pickup.Info.Serial} allowed={ev.IsAllowed} " +
                 $"player={ev.Player?.Nickname ?? "unknown"} custom={CustomItem.Of(pickup.Info.Serial)?.GetType().Name ?? "none"}");
    }
}

/// <summary>
/// 一時的な診断: Pickup 検索が完了するまでの各段階をログに出す。
/// </summary>
[HarmonyPatch(typeof(PickupSearchCompletor), nameof(PickupSearchCompletor.ValidateStart))]
internal static class PickupSearchValidateStartDiagnosticPatch
{
    [HarmonyPostfix]
    private static void Postfix(PickupSearchCompletor __instance, ref bool __result)
    {
        var pickup = __instance.TargetPickup;
        if (pickup == null || pickup.Info.ItemId != ItemType.Painkillers)
            return;

        var hub = __instance.Hub;
        float distanceSqr = hub == null
            ? -1f
            : (pickup.transform.position - hub.transform.position).sqrMagnitude;
        Log.Warn($"[PickupDiag] ValidateStart serial={pickup.Info.Serial} result={__result} " +
                 $"distanceSqr={distanceSqr:0.###} maxDistanceSqr={__instance.MaxDistanceSqr:0.###} " +
                 $"distanceValid={__instance.ValidateDistance()} pickupPos={pickup.transform.position} " +
                 $"playerPos={hub?.transform.position.ToString() ?? "unknown"} " +
                 $"locked={pickup.Info.Locked} custom={CustomItem.Of(pickup.Info.Serial)?.GetType().Name ?? "none"}");
    }
}

[HarmonyPatch(typeof(PickupSearchCompletor), nameof(PickupSearchCompletor.ValidateAny))]
internal static class PickupSearchValidateAnyDiagnosticPatch
{
    [HarmonyPostfix]
    private static void Postfix(PickupSearchCompletor __instance, ref bool __result)
    {
        var pickup = __instance.TargetPickup;
        if (pickup == null || pickup.Info.ItemId != ItemType.Painkillers)
            return;

        var hub = __instance.Hub;
        Log.Warn($"[PickupDiag] ValidateAny serial={pickup.Info.Serial} result={__result} " +
                 $"locked={pickup.Info.Locked} inUse={pickup.Info.InUse} " +
                 $"role={hub?.roleManager?.CurrentRole?.GetType().Name}");
    }
}

[HarmonyPatch(typeof(ItemSearchCompletor), nameof(ItemSearchCompletor.ValidateAny))]
internal static class ItemSearchValidateAnyDiagnosticPatch
{
    [HarmonyPostfix]
    private static void Postfix(ItemSearchCompletor __instance, ref bool __result)
    {
        var pickup = __instance.TargetPickup;
        if (pickup == null || pickup.Info.ItemId != ItemType.Painkillers)
            return;

        var hub = __instance.Hub;
        Log.Warn($"[PickupDiag] ItemValidateAny serial={pickup.Info.Serial} result={__result} " +
                 $"locked={pickup.Info.Locked} inUse={pickup.Info.InUse} " +
                 $"role={hub?.roleManager?.CurrentRole?.GetType().Name}");
    }
}

[HarmonyPatch(typeof(PickupSearchCompletor), nameof(PickupSearchCompletor.ValidateUpdate))]
internal static class PickupSearchValidateUpdateDiagnosticPatch
{
    [HarmonyPostfix]
    private static void Postfix(PickupSearchCompletor __instance, ref bool __result)
    {
        var pickup = __instance.TargetPickup;
        if (pickup == null || pickup.Info.ItemId != ItemType.Painkillers)
            return;

        var hub = __instance.Hub;
        float distance = hub == null ? -1f : (pickup.transform.position - hub.transform.position).magnitude;
        Log.Warn($"[PickupDiag] ValidateUpdate serial={pickup.Info.Serial} result={__result} " +
                 $"distance={distance:0.##} maxSqr={__instance.MaxDistanceSqr:0.##} locked={pickup.Info.Locked}");
    }
}

[HarmonyPatch(typeof(PickupSearchCompletor), nameof(PickupSearchCompletor.Complete))]
internal static class PickupSearchCompleteDiagnosticPatch
{
    [HarmonyPrefix]
    private static void Prefix(PickupSearchCompletor __instance)
    {
        var pickup = __instance.TargetPickup;
        if (pickup == null || pickup.Info.ItemId != ItemType.Painkillers)
            return;

        Log.Warn($"[PickupDiag] Complete REACHED serial={pickup.Info.Serial}");
    }
}
