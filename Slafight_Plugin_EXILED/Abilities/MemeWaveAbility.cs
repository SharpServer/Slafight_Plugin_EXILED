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

    // 完全デフォルト
    public MemeWaveAbility(Player owner)
        : base(owner) { }

    // コマンドなどから上書きしたいとき用
    public MemeWaveAbility(Player owner, float cooldownSeconds)
        : base(owner, cooldownSeconds) { }

    public MemeWaveAbility(Player owner, float cooldownSeconds, int maxUses)
        : base(owner, cooldownSeconds, maxUses) { }

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