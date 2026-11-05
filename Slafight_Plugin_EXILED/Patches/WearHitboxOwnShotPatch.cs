using HarmonyLib;
using InventorySystem.Items.Firearms.Modules;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.Patches;

/// <summary>
/// 発砲中は撃った本人のモデルの当たり判定を切ります。バニラが本人の体に行う自傷防止と同じです。
/// </summary>
[HarmonyPatch(typeof(HitscanHitregModuleBase), "SetHitboxes")]
internal static class WearHitboxOwnShotPatch
{
    [HarmonyPostfix]
    private static void Postfix(HitscanHitregModuleBase __instance, bool restore)
    {
        ReferenceHub owner = __instance.Owner;

        if (owner != null)
            WearManager.SetOwnShotHitboxes(owner.netId, restore);
    }
}
