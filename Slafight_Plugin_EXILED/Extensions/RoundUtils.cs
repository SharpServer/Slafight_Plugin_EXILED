using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.RoundVictory;
using Slafight_Plugin_EXILED.CustomRoles;

namespace Slafight_Plugin_EXILED.Extensions;

public static class RoundUtils
{
    /// <summary>
    /// 特殊勝利としてラウンドを終了させる。
    /// 呼び出し時に IsSpecialWinEnding を true にし、RoundLock 解除は呼び出し側で行う想定。
    /// </summary>
    public static void EndRound(this CTeam team, string specificReason = null)
    {
        CustomRolesHandler.EndRound(team, specificReason);
    }

    /// <summary>
    /// vanilla の終了判定を止めるべき独自勝利勢力の生存者かを返します。
    /// </summary>
    /// <remarks>
    /// 実際の対象勢力は <see cref="RoundVictoryDefinitions.RequiresVanillaEndLock"/> で管理します。
    /// </remarks>
    public static bool HasSpecificWinMethod(this Player player)
    {
        var info = player.GetRoleInfo();
        if (info is { Vanilla: RoleTypeId.Tutorial, Custom: CRoleTypeId.None }) return false;
        return RoundVictoryDefinitions.RequiresVanillaEndLock(player);
    }
}
