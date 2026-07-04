using System;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Toys;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles.FirstPersonControl;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

/// <summary>
/// WaterWarrior 特有武器その2。直撃した相手を吹き飛ばし、長時間SinkHoleへ引きずり込む高圧放水砲。
/// </summary>
public class HydroCannon : CItemWeapon
{
    private const byte SinkHoleIntensity  = 60;
    private const float SinkHoleDuration  = 14f;
    private const float KnockbackPower    = 5f;
    private const float UpwardPower       = 2f;
    private const int BurstCount          = 4;
    private const float BurstLifetime     = 0.3f;

    private static readonly Color WaterColor = new(0.2f, 0.75f, 1f, 0.5f);

    public override string DisplayName => "ハイドロ・キャノン";
    public override string Description =>
        "水鉄砲を悪魔改造した高圧放水砲。直撃した相手を吹き飛ばし、長時間SinkHoleへ引きずり込む。";

    protected override string UniqueKey => "HydroCannon";
    protected override ItemType BaseItem => ItemType.GunFRMG0;

    protected override float   Damage       => 22f;
    protected override byte    MagazineSize => 60;
    protected override Vector3 Scale        => new(1.05f, 1f, 1.3f);

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => WaterColor;

    protected override void OnHurtingOthers(HurtingEventArgs ev)
    {
        if (ev.Player != null)
        {
            ev.Player.EnableEffect(EffectType.SinkHole, SinkHoleIntensity, SinkHoleDuration);
            Knockback(ev.Attacker, ev.Player);
        }

        base.OnHurtingOthers(ev);
    }

    protected override void OnShot(ShotEventArgs ev)
    {
        base.OnShot(ev);
        SpawnSplashBurst(ev.Position);
    }

    private static void Knockback(Player? attacker, Player target)
    {
        if (attacker == null) return;
        if (target.ReferenceHub?.roleManager?.CurrentRole is not IFpcRole fpcRole ||
            fpcRole.FpcModule?.ModuleReady != true)
            return;

        var direction = target.Position - attacker.Position;
        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.forward;

        fpcRole.FpcModule.Motor.Velocity =
            new Vector3(direction.x * KnockbackPower, UpwardPower, direction.z * KnockbackPower);
    }

    private static void SpawnSplashBurst(Vector3 position)
    {
        for (int i = 0; i < BurstCount; i++)
        {
            var offset = UnityEngine.Random.insideUnitSphere * 0.3f;
            var scale = Vector3.one * (0.25f + i * 0.1f);

            try
            {
                var burst = Primitive.Create(
                    PrimitiveType.Sphere, position + offset, Vector3.zero, scale, true, WaterColor);
                burst.Collidable = false;

                Timing.CallDelayed(BurstLifetime, () =>
                {
                    try { burst.Destroy(); }
                    catch { /* ignored */ }
                });
            }
            catch (Exception ex)
            {
                Log.Error($"HydroCannon splash burst spawn failed: {ex}");
            }
        }
    }
}
