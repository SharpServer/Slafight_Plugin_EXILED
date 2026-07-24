using HarmonyLib;
using PlayerRoles.Spectating;

namespace Slafight_Plugin_EXILED.Patches;

/// <summary>
/// バニラの SpectatableModuleBase.TargetHub は、所有者(ReferenceHub)を失った
/// ロール(例: Npc.Spawn で生成した FacilityGuardRole が破棄処理中の一瞬)を
/// 参照すると InvalidOperationException を投げる。
/// SpectatorRole.Update() -> NextTarget() は例外を捕捉しないため、
/// _anySpectatorSelected が true にならず毎フレーム同じ例外を吐き続け、
/// 観戦者が対象を切り替えられなくなる(一斉大量死＋NPC破棄タイミングが重なると再現)。
/// 呼び出し側は既に TargetHub の戻り値を null チェックして扱っているため、
/// 例外を握りつぶして null を返すだけで安全に同じセマンティクスを維持できる。
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
