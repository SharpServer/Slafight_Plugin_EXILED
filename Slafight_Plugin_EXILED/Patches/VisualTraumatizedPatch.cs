using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using CustomPlayerEffects;
using Exiled.API.Features;
using HarmonyLib;
using PlayerRoles.PlayableScps.Scp106;
using Slafight_Plugin_EXILED.CustomEffects;

namespace Slafight_Plugin_EXILED.Patches;

[HarmonyPatch(typeof(Scp106Attack), nameof(Scp106Attack.ServerShoot))]
public static class Scp106VisualTraumatizedPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ServerShootTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = instructions.ToList();
        MethodInfo isEnabledGetter = AccessTools.PropertyGetter(typeof(StatusEffectBase), nameof(StatusEffectBase.IsEnabled));
        MethodInfo replacement = AccessTools.Method(typeof(Scp106VisualTraumatizedPatch), nameof(ShouldTriggerTraumatizedKill));
        bool replaced = false;

        for (int i = 0; i < codes.Count; i++)
        {
            CodeInstruction instruction = codes[i];

            if (!replaced &&
                instruction.Calls(isEnabledGetter) &&
                i > 0 &&
                IsGetEffectCall(codes[i - 1], typeof(Traumatized)))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
                replaced = true;
            }

            yield return instruction;
        }

        if (!replaced)
        {
            Log.Error(
                $"[{nameof(Scp106VisualTraumatizedPatch)}] Traumatized IsEnabled call was not found in " +
                $"{nameof(Scp106Attack)}.{nameof(Scp106Attack.ServerShoot)}; patch was not applied.");
        }
    }

    public static bool ShouldTriggerTraumatizedKill(Traumatized traumatized)
    {
        return traumatized != null &&
               traumatized.IsEnabled &&
               !VisualTraumatized.ShouldSuppressScp106Kill(traumatized.Hub);
    }

    private static bool IsGetEffectCall(CodeInstruction instruction, Type effectType)
    {
        if (instruction.operand is not MethodInfo method || !method.IsGenericMethod)
            return false;

        if (method.Name != nameof(PlayerEffectsController.GetEffect) ||
            method.DeclaringType != typeof(PlayerEffectsController))
        {
            return false;
        }

        Type[] arguments = method.GetGenericArguments();
        return arguments.Length == 1 && arguments[0] == effectType;
    }
}

[HarmonyPatch(typeof(Traumatized))]
public static class VisualTraumatizedLifecyclePatch
{
    [HarmonyPatch(nameof(Traumatized.AllowEnabling), MethodType.Getter)]
    [HarmonyPrefix]
    private static bool AllowEnablingPrefix(Traumatized __instance, ref bool __result)
    {
        if (!VisualTraumatized.ShouldSuppressScp106Kill(__instance.Hub))
            return true;

        __result = true;
        return false;
    }

    [HarmonyPatch(nameof(Traumatized.Enabled))]
    [HarmonyPrefix]
    private static bool EnabledPrefix(Traumatized __instance)
    {
        return !VisualTraumatized.ShouldSuppressScp106Kill(__instance.Hub);
    }

    [HarmonyPatch(nameof(Traumatized.OnServerRoleChanged))]
    [HarmonyPrefix]
    private static bool OnServerRoleChangedPrefix(Traumatized __instance)
    {
        return !VisualTraumatized.ShouldSuppressScp106Kill(__instance.Hub);
    }
}
