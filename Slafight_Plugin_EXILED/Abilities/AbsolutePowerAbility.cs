using System.Linq;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Abilities;

public class AbsolutePowerAbility : AbilityBase
{
    // AbilityBase 抽象プロパティの実装（デフォルト値）
    protected override float DefaultCooldown => 120f;
    protected override int DefaultMaxUses => -1;

    // 完全デフォルト
    public AbsolutePowerAbility(Player owner)
        : base(owner) { }

    // クールダウンだけ変える
    public AbsolutePowerAbility(Player owner, float cooldownSeconds)
        : base(owner, cooldownSeconds, null) { }

    // 両方カスタム
    public AbsolutePowerAbility(Player owner, float cooldownSeconds, int maxUses)
        : base(owner, cooldownSeconds, maxUses) { }

    protected override bool CanActivate(Player player, out string failureReason)
    {
        if (!base.CanActivate(player, out failureReason))
            return false;

        if (Door.List.Count(x => Vector3.Distance(x.Position, player.Position) <= 3.5f) < 1)
        {
            failureReason = "近くに利用可能なドアがありません";
            return false;
        }
        
        return true;
    }

    protected override void ExecuteAbility(Player player)
    {
        var door = Door.List.FirstOrDefault(x => Vector3.Distance(x.Position, player.Position) <= 3.5f);
        if (door is BreakableDoor breakableDoor)
        {
            breakableDoor.ForceBreak();
        }
    }
}