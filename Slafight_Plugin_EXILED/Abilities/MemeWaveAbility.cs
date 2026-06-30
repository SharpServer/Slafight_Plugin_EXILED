using System.Linq;
using Exiled.API.Features;
using Exiled.API.Features.Roles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.Abilities;

public class MemeWaveAbility : AbilityBase
{
    // AbilityBase の抽象プロパティを実装
    protected override float DefaultCooldown => 150f;
    protected override int DefaultMaxUses => -1;

    protected override bool CanActivate(Player player, out string failureReason)
    {
        if (!base.CanActivate(player, out failureReason))
            return false;

        if (!Player.List.Any(target =>
                target != null &&
                target != player &&
                target.IsAlive &&
                target.GetCustomRole() == CRoleTypeId.AraOrun &&
                target.Role is Scp079Role))
        {
            failureReason = "干渉可能な対象が存在しません。";
            return false;
        }

        return true;
    }

    protected override void ExecuteAbility(Player player)
    {
        foreach (var targetPlayer in Player.List)
        {
            if (targetPlayer == null) continue;
            if (targetPlayer == player) continue;
            if (targetPlayer.GetCustomRole() is not CRoleTypeId.AraOrun) continue;
            if (targetPlayer.Role is Scp079Role scp079Role)
            {
                scp079Role.Level--;
            }
        }
    }
}
