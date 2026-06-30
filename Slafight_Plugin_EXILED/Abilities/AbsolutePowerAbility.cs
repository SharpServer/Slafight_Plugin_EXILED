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

    protected override bool CanActivate(Player player, out string failureReason)
    {
        if (!base.CanActivate(player, out failureReason))
            return false;

        if (Door.List.Count(x => x is BreakableDoor && Vector3.Distance(x.Position, player.Position) <= 3.5f) < 1)
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
