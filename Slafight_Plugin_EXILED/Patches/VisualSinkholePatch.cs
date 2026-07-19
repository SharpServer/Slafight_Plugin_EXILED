using CustomPlayerEffects;
using HarmonyLib;
using PlayerRoles.FirstPersonControl.Thirdperson;
using Slafight_Plugin_EXILED.CustomEffects;

namespace Slafight_Plugin_EXILED.Patches;

[HarmonyPatch(typeof(Sinkhole))]
public static class VisualSinkholePatch
{
    [HarmonyPatch(nameof(Sinkhole.AllowEnabling), MethodType.Getter)]
    [HarmonyPrefix]
    private static bool AllowEnablingPrefix(Sinkhole __instance, ref bool __result)
    {
        if (!VisualSinkhole.TryGetOwner(__instance.Hub, out _))
            return true;

        __result = true;
        return false;
    }

    [HarmonyPatch(nameof(Sinkhole.MovementModifierActive), MethodType.Getter)]
    [HarmonyPostfix]
    private static void MovementModifierActivePostfix(Sinkhole __instance, ref bool __result)
    {
        if (VisualSinkhole.TryGetOwner(__instance.Hub, out _))
            __result = false;
    }

    [HarmonyPatch(nameof(Sinkhole.StaminaModifierActive), MethodType.Getter)]
    [HarmonyPostfix]
    private static void StaminaModifierActivePostfix(Sinkhole __instance, ref bool __result)
    {
        if (VisualSinkhole.TryGetOwner(__instance.Hub, out _))
            __result = false;
    }

    [HarmonyPatch(nameof(Sinkhole.SprintingDisabled), MethodType.Getter)]
    [HarmonyPostfix]
    private static void SprintingDisabledPostfix(Sinkhole __instance, ref bool __result)
    {
        if (VisualSinkhole.TryGetOwner(__instance.Hub, out _))
            __result = false;
    }

    [HarmonyPatch(nameof(Sinkhole.ProcessFootstepOverrides), typeof(AnimatedCharacterModel), typeof(float))]
    [HarmonyPrefix]
    private static bool ProcessFootstepOverridesPrefix(Sinkhole __instance, float dis, ref float __result)
    {
        if (!VisualSinkhole.TryGetOwner(__instance.Hub, out VisualSinkhole effect) ||
            effect.FootstepOverridesEnabled)
        {
            return true;
        }

        __result = dis;
        return false;
    }
}
