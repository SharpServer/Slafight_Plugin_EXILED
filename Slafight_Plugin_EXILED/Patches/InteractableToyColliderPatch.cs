using AdminToys;
using HarmonyLib;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Patches;

[HarmonyPatch(typeof(InvisibleInteractableToy), nameof(InvisibleInteractableToy.SetCollider))]
public static class InteractableToyColliderPatch
{
    [HarmonyPostfix]
    private static void Postfix(InvisibleInteractableToy __instance)
    {
        foreach (Collider collider in __instance.GetComponents<Collider>())
            collider.isTrigger = true;
    }
}
