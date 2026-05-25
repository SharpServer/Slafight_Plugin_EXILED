using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Abilities;

public class SoundOfFifthAbility : AbilityBase
{
    // AbilityBase の抽象プロパティを実装
    protected override float DefaultCooldown => 20f;
    protected override int DefaultMaxUses => -1;

    // 完全デフォルト
    public SoundOfFifthAbility(Player owner)
        : base(owner) { }

    // コマンドなどから上書きしたいとき用
    public SoundOfFifthAbility(Player owner, float cooldownSeconds)
        : base(owner, cooldownSeconds) { }

    public SoundOfFifthAbility(Player owner, float cooldownSeconds, int maxUses)
        : base(owner, cooldownSeconds, maxUses) { }

    protected override void ExecuteAbility(Player player)
    {
        foreach (var targetPlayer in Player.List)
        {
            if (targetPlayer == null) continue;
            if (targetPlayer == player) continue;
            if (targetPlayer.HasFlag(SpecificFlagType.AntiMemeEffectDisabled)) continue;
            if (!(Vector3.Distance(targetPlayer.Position, player.Position) <= 5f)) continue;
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
}