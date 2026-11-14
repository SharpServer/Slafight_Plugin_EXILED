using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Extensions;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomTeams;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Abilities;

/// <summary>
/// SCP-3005 の範囲効果です。近くの敵を反ミームで侵し、第五教会の仲間を奮い立たせます。
/// 敵への効果は近いほど強く、長く続きます。
/// </summary>
public sealed class Scp3005Sound : AbilityBase
{
    private const float Range = 12f;

    public override string Name => "第五からの音";

    public override string Description => "近くの者ほど強く反ミームに侵し、第五教会の仲間を奮い立たせる。";

    public override float Cooldown => 20f;

    protected override bool CanUse(out string failureReason)
    {
        if (!base.CanUse(out failureReason))
            return false;

        if (!Targets().Any())
        {
            failureReason = "効果範囲内に対象が存在しません。";

            return false;
        }

        return true;
    }

    protected override void OnUsed()
    {
        foreach (Player target in Targets().ToList())
        {
            if (target.IsInTeam<FifthistTeam>())
            {
                target.EnableEffect(EffectType.Invigorated, 5f);
                continue;
            }

            // 0m で 1、範囲の端で 0。
            float power = 1f - Mathf.Clamp01(Vector3.Distance(target.Position, Player.Position) / Range);
            float duration = Mathf.Lerp(10f, 45f, power);

            target.ExplodeEffect(ProjectileType.Flashbang);
            target.EnableEffect<Flashed>(Mathf.Lerp(0.5f, 2.5f, power));
            target.EnableEffect<Deafened>(255, duration);
            target.EnableEffect<Hemorrhage>(255, duration);
            target.EnableEffect<Blindness>((byte)Mathf.RoundToInt(Mathf.Lerp(10f, 40f, power)), duration);
            target.EnableEffect<Blurred>(255, duration);
        }
    }

    private IEnumerable<Player> Targets() => Player.List.Where(target =>
        target.IsSafePlayer() && target != Player && target.IsAlive &&
        !target.HasFlag(SpecificFlagType.AntiMemeEffectDisabled) &&
        Vector3.Distance(target.Position, Player.Position) <= Range);
}
