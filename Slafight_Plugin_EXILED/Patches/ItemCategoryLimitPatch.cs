using System;
using HarmonyLib;
using InventorySystem.Configs;
using InventorySystem.Searching;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.Patches;

/// <summary>
/// ゲーム本体のカテゴリ上限へ Slafight の積み上げ式補正を合成します。
/// </summary>
[HarmonyPatch(
    typeof(InventoryLimits),
    nameof(InventoryLimits.GetCategoryLimit),
    typeof(ItemCategory),
    typeof(ReferenceHub))]
internal static class ItemCategoryLimitPatch
{
    [HarmonyPostfix]
    private static void Postfix(ItemCategory category, ReferenceHub player, ref sbyte __result) =>
        __result = ItemLimitApi.ApplyCategoryLimit(__result, player, category);
}

/// <summary>
/// ValidateAny 内の上限問い合わせに、現在拾おうとしている Pickup のシリアルを渡します。
/// Finalizer で必ず破棄するため、例外や早期 return でも次の判定へ漏れません。
/// </summary>
[HarmonyPatch(typeof(ItemSearchCompletor), nameof(ItemSearchCompletor.ValidateAny))]
internal static class ItemCategoryLimitPickupContextPatch
{
    [HarmonyPrefix]
    private static void Prefix(ItemSearchCompletor __instance) =>
        ItemLimitApi.EnterPickupCheck(__instance?.TargetPickup?.Info.Serial ?? 0);

    [HarmonyFinalizer]
    private static Exception Finalizer(Exception __exception)
    {
        ItemLimitApi.ExitPickupCheck();
        return __exception;
    }
}
