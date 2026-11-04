using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Extensions;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.CustomTeams;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Abilities;

/// <summary>SCP-3005 の前方攻撃です。少し遅れて正面の敵へ第五的な被害を与えます。</summary>
public sealed class Scp3005Mindblast : AbilityBase
{
    public override string Name => "第五思壊線";

    public override string Description => "第五的な光を放ち、正面の敵をなきものにする。";

    public override float Cooldown => 5f;

    protected override void OnUsed()
    {
        Vector3 direction = Player.CameraTransform.forward.normalized;
        Vector3 origin = Player.Position + Vector3.up * 0.5f;

        // 発射までの間に役職が外れても、Scope が遅延を止めるので判定は不要です。
        Scope.Delay(0.8f, owner => Strike(owner, origin, direction));
    }

    private static void Strike(Player owner, Vector3 origin, Vector3 direction)
    {
        Vector3 impact = origin + direction * 25f;

        foreach (Player target in Player.List)
        {
            if (!target.IsSafePlayer() || target == owner || !target.IsAlive ||
                target.IsInTeam<FifthistTeam>() || target.IsInTeam<ScpTeam>()) continue;

            if (Vector3.Distance(target.Position, impact) > 2.5f) continue;

            target.EnableEffect<Burned>(255, 3f);
            target.EnableEffect<Concussed>(255, 3f);
            target.Hurt(owner, 30f, DamageType.Unknown, null, "<color=#ff00fa>第五的</color>な力による影響");
            owner.ShowHitMarker();
        }
    }
}
