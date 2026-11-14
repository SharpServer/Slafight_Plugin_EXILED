using CustomPlayerEffects;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.CustomItems.Weapons;
using Slafight_Plugin_EXILED.CustomTeams;
using Slafight_Plugin_EXILED.API.Core.Extensions;

namespace Slafight_Plugin_EXILED.Abilities;

/// <summary>SCP-3005 の第五思壊線です。正面へ光弾を飛ばし、触れた敵を焼きます。</summary>
public sealed class Scp3005Mindblast : AbilityBase
{
    public override string Name => "第五思壊線";

    public override string Description => "第五的な光を放ち、正面の敵をなきものにする。";

    public override float Cooldown => 5f;

    protected override void OnUsed()
    {
        Mindblaster.Launch(
            Player,
            Player.Position + UnityEngine.Vector3.up * 0.5f,
            damage: 30f,
            effectDuration: 35f,
            canHit: target => !target.IsInTeam<FifthistTeam>());
    }
}
