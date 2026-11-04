using CustomPlayerEffects;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Extensions;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.CustomTeams;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Abilities;

/// <summary>SCP-3005 の範囲効果です。近くの敵を苦しめ、第五教会の仲間を奮い立たせます。</summary>
public sealed class Scp3005Sound : AbilityBase
{
    public override string Name => "第五からの音";

    public override string Description => "近くの者を反ミームに侵し、第五教会の仲間を奮い立たせる。";

    public override float Cooldown => 20f;

    protected override void OnUsed()
    {
        Player owner = Player;

        foreach (Player target in Player.List)
        {
            if (!target.IsSafePlayer() || target == owner || !target.IsAlive ||
                Vector3.Distance(target.Position, owner.Position) > 12f) continue;

            if (target.IsInTeam<FifthistTeam>())
            {
                target.EnableEffect<Invigorated>(5, 5f);
                continue;
            }

            target.EnableEffect<Flashed>(2.5f);
            target.EnableEffect<Deafened>(255, 8f);
            target.EnableEffect<Blurred>(255, 8f);
            target.EnableEffect<Hemorrhage>(100, 8f);
        }
    }
}
