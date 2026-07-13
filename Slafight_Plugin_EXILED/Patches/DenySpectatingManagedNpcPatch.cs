using Exiled.API.Features;
using HarmonyLib;
using PlayerRoles.Spectating;
using Slafight_Plugin_EXILED.API.Features;

namespace Slafight_Plugin_EXILED.Patches;

/// <summary>
/// バニラの SpectatorNetworking メッセージハンドラは Overwatch(管理者観戦) の場合のみ
/// IsSpectatingAllowed / IsSpectatable を無視して観戦対象の切り替えを許可してしまう。
/// InternalNpcRegistry に登録された内部処理用 Npc（HIDTurret / Tentacle / TeamNpc 等）は
/// 観戦させるとクライアントがクラッシュすることがあるため、
/// 実際に切り替えが書き込まれる SyncedSpectatedNetId の setter 側で対象を問わず拒否する。
/// </summary>
[HarmonyPatch(typeof(SpectatorRole), nameof(SpectatorRole.SyncedSpectatedNetId), MethodType.Setter)]
internal static class DenySpectatingManagedNpcPatch
{
    [HarmonyPrefix]
    private static bool Prefix(uint value)
    {
        if (value == 0)
            return true;

        if (!ReferenceHub.TryGetHubNetID(value, out var targetHub))
            return true;

        Player? targetPlayer = Player.Get(targetHub);
        if (targetPlayer != null && InternalNpcRegistry.IsManaged(targetPlayer.Id))
            return false;

        return true;
    }
}
