using System;
using Exiled.API.Features;
using HarmonyLib;
using InventorySystem.Items.Usables.Scp1344;
using Slafight_Plugin_EXILED.CustomItems;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Patches;

[HarmonyPatch(typeof(Scp1344Item))]
public static class Scp1344NvgBlindnessPatch
{
    [HarmonyPatch(nameof(Scp1344Item.ServerUpdateActive))]
    [HarmonyPrefix]
    private static bool ServerUpdateActivePrefix(Scp1344Item __instance)
    {
        if (!NvgManager.IsManagedNvg(__instance))
            return true;

        try
        {
            __instance._useTime += Time.deltaTime;

            if (__instance._useTime >= Scp1344Item.ActivationItemDeselectionTime
                && __instance._useTime < Scp1344Item.ActivationTransitionTime
                && __instance.IsEquipped)
            {
                __instance.OwnerInventory.ServerSelectItem(0);
            }

            return false;
        }
        catch (Exception ex)
        {
            Log.Warn($"[Scp1344NvgBlindnessPatch] ServerUpdateActive failed: {ex.Message}");
            return true;
        }
    }

    [HarmonyPatch(nameof(Scp1344Item.ServerChangeStatus))]
    [HarmonyPostfix]
    private static void ServerChangeStatusPostfix(Scp1344Item __instance, Scp1344Status status)
    {
        switch (status)
        {
            case Scp1344Status.Active:
            case Scp1344Status.Dropping:
            case Scp1344Status.CancelingDeactivation:
            case Scp1344Status.Idle:
                NvgManager.ReapplyManagedBlindness(__instance);
                break;
        }
    }

    [HarmonyPatch(nameof(Scp1344Item.ServerUpdateDeactivating))]
    [HarmonyPostfix]
    private static void ServerUpdateDeactivatingPostfix(Scp1344Item __instance)
        => NvgManager.ReapplyManagedBlindness(__instance);

    [HarmonyPatch(nameof(Scp1344Item.ActivateFinalEffects))]
    [HarmonyPrefix]
    private static bool ActivateFinalEffectsPrefix(Scp1344Item __instance)
    {
        if (!NvgManager.IsManagedNvg(__instance))
            return true;

        try
        {
            __instance.Scp1344Effect.IsEnabled = false;
            __instance.SeveredEyesEffect.IsEnabled = false;
            NvgManager.ReapplyManagedBlindness(__instance);
            return false;
        }
        catch (Exception ex)
        {
            Log.Warn($"[Scp1344NvgBlindnessPatch] ActivateFinalEffects failed: {ex.Message}");
            return true;
        }
    }
}
