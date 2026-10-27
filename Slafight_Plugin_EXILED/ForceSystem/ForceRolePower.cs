using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.ForceSystem;

/// <summary>
/// TopLead 選出で使う役職の優先度です。大きいほど上位です。
/// </summary>
public static class ForceRolePower
{
    /// <summary>
    /// このプレイヤーの役職優先度です。カスタム役職が 0 ならバニラの値を使います。
    /// </summary>
    public static int Of(Player player)
    {
        if (player is null) return 0;

        if (CustomRole.Of(player) is { } customRole && customRole.ForceRolePower > 0)
            return customRole.ForceRolePower;

        return Of(player.Role?.Type ?? RoleTypeId.None);
    }

    /// <summary>
    /// バニラ役職の優先度です (Private 1 / Specialist・Sergeant 2 / Captain 3)。
    /// </summary>
    public static int Of(RoleTypeId role) => role switch
    {
        RoleTypeId.NtfCaptain => 3,
        RoleTypeId.NtfSergeant or RoleTypeId.NtfSpecialist => 2,
        RoleTypeId.NtfPrivate => 1,

        RoleTypeId.ChaosRepressor => 2,
        RoleTypeId.ChaosMarauder or RoleTypeId.ChaosRifleman or RoleTypeId.ChaosConscript => 1,

        RoleTypeId.FacilityGuard => 1,

        _ => 0,
    };
}
