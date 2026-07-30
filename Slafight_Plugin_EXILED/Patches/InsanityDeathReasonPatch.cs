#nullable enable

using HarmonyLib;
using PlayerRoles.Ragdolls;
using PlayerRoles.Spectating;
using PlayerStatsSystem;
using Slafight_Plugin_EXILED.CustomEffects;

namespace Slafight_Plugin_EXILED.Patches;

/// <summary>
/// 発狂状態（<see cref="Insanity"/>）のまま死んだプレイヤーの死因表示を、
/// 実際の死因に関係なく発狂的な死に方テキストへ差し替える。
/// <para>
/// 差し替えるのは表示だけ。<see cref="PlayerStats.KillPlayer"/> に渡された
/// <see cref="DamageHandlerBase"/> 自体は書き換えないので、EXILED の Dying / Died や
/// LabAPI の Death イベントが受け取る加害者情報（キル数集計・勝利判定）はそのまま残る。
/// </para>
/// <para>
/// <see cref="PlayerStats.KillPlayer"/> は同期実行で、内部の
/// <see cref="RagdollManager.ServerSpawnRagdoll"/> と
/// <see cref="SpectatorRole.ServerSetData"/> もその中で完結する。そのため
/// 「今死んでいるプレイヤー」を 1 スロットだけ保持すれば足りる。
/// </para>
/// </summary>
[HarmonyPatch]
internal static class InsanityDeathReasonPatch
{
    private static ReferenceHub? _dyingHub;
    private static string? _deathReason;

    [HarmonyPatch(typeof(PlayerStats), nameof(PlayerStats.KillPlayer), typeof(DamageHandlerBase))]
    [HarmonyPrefix]
    private static void CaptureInsaneDeath(PlayerStats __instance)
    {
        ReferenceHub hub = __instance._hub;

        if (hub == null || !IsInsane(hub))
            return;

        _dyingHub = hub;
        _deathReason = Insanity.PickDeathReason();
    }

    [HarmonyPatch(typeof(PlayerStats), nameof(PlayerStats.KillPlayer), typeof(DamageHandlerBase))]
    [HarmonyFinalizer]
    private static void ClearInsaneDeath()
    {
        _dyingHub = null;
        _deathReason = null;
    }

    /// <summary>死体を調べたときに出る死因。</summary>
    [HarmonyPatch(typeof(RagdollManager), nameof(RagdollManager.ServerSpawnRagdoll))]
    [HarmonyPrefix]
    private static void OverrideRagdollReason(ReferenceHub owner, ref DamageHandlerBase handler)
    {
        if (TryGetReason(owner, out string reason))
            handler = new CustomReasonDamageHandler(reason);
    }

    /// <summary>死んだ本人のデスクリーンに出る死因。</summary>
    [HarmonyPatch(typeof(SpectatorRole), nameof(SpectatorRole.ServerSetData))]
    [HarmonyPrefix]
    private static void OverrideDeathScreenReason(SpectatorRole __instance, ref DamageHandlerBase dhb)
    {
        if (!__instance.TryGetOwner(out ReferenceHub hub))
            return;

        if (TryGetReason(hub, out string reason))
            dhb = new CustomReasonDamageHandler(reason);
    }

    private static bool TryGetReason(ReferenceHub hub, out string reason)
    {
        if (hub != null && hub == _dyingHub && !string.IsNullOrEmpty(_deathReason))
        {
            reason = _deathReason!;
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static bool IsInsane(ReferenceHub hub)
    {
        return hub.playerEffectsController != null &&
               hub.playerEffectsController.TryGetEffect(out Insanity insanity) &&
               insanity.IsEnabled;
    }
}
