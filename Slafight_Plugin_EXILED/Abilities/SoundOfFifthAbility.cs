using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Abilities;

public class SoundOfFifthAbility : OptionAbilityBase
{
    private const float CloseRange = 5f;
    private const float FarRange = 12f;

    // AbilityBase の抽象プロパティを実装
    protected override float DefaultCooldown => 20f;
    protected override int DefaultMaxUses => -1;

    protected override IReadOnlyList<AbilityOption> DefineOptions() =>
    [
        Option("close", "近距離", "5m以内の対象へ強く干渉します。"),
        Option("far", "遠距離", "12m以内の対象へ広く干渉します。"),
    ];

    protected override bool CanUseOption(Player player, AbilityOption option, out string failureReason)
    {
        failureReason = string.Empty;
        var range = GetRange(option);
        if (!Player.List.Any(target =>
                target != null &&
                target != player &&
                target.IsAlive &&
                !target.HasFlag(SpecificFlagType.AntiMemeEffectDisabled) &&
                Vector3.Distance(target.Position, player.Position) <= range))
        {
            failureReason = "効果範囲内に対象が存在しません。";
            return false;
        }

        return true;
    }

    protected override void UseOption(Player player, AbilityOption option)
    {
        var range = GetRange(option);
        foreach (var targetPlayer in Player.List)
        {
            if (targetPlayer == null) continue;
            if (targetPlayer == player) continue;
            if (targetPlayer.HasFlag(SpecificFlagType.AntiMemeEffectDisabled)) continue;
            if (!(Vector3.Distance(targetPlayer.Position, player.Position) <= range)) continue;
            if (targetPlayer.GetTeam() != CTeam.Fifthists)
            {
                targetPlayer.Explode(ProjectileType.Flashbang,player);
                targetPlayer.EnableEffect<Deafened>(255, 45);
                targetPlayer.EnableEffect<Hemorrhage>(255, 45);
                targetPlayer.EnableEffect<Blindness>(40, 45);
                targetPlayer.EnableEffect<Blurred>(255, 45);
            }
            else
            {
                targetPlayer.EnableEffect(EffectType.Invigorated, 5f);
            }
        }
    }

    private static float GetRange(AbilityOption option)
        => option.Is("far") ? FarRange : CloseRange;
}
