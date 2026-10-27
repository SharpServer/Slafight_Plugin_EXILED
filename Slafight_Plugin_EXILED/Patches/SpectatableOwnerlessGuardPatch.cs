using HarmonyLib;
using PlayerRoles.Spectating;

namespace Slafight_Plugin_EXILED.Patches;

/// <summary>
/// 所有者を失ったロールの TargetHub 参照で例外を投げず null を返し、観戦者の対象切り替えが止まるのを防ぐ。
/// </summary>
[HarmonyPatch(typeof(SpectatableModuleBase), nameof(SpectatableModuleBase.TargetHub), MethodType.Getter)]
internal static class SpectatableOwnerlessGuardPatch
{
    [HarmonyPrefix]
    private static bool Prefix(SpectatableModuleBase __instance, ref ReferenceHub __result)
    {
        if (__instance == null)
            return true;

        if (!__instance.MainRole.TryGetOwner(out ReferenceHub hub) || hub == null)
        {
            __result = null;
            return false;
        }

        return true;
    }
}
