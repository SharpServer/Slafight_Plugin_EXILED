using System;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Toys;
using Exiled.Events.EventArgs.Player;
using MEC;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

/// <summary>
/// WaterWarrior 特有武器その1。当てた相手を暫くSinkHoleへ引きずり込む水鉄砲。
/// </summary>
public class AquaBlaster : CItemWeapon
{
    private const byte SinkHoleIntensity  = 45;
    private const float SinkHoleDuration  = 10f;
    private const byte SlownessIntensity  = 25;
    private const float SlownessDuration  = 6f;
    private const int DropletCount        = 5;
    private const float DropletLifetime   = 0.25f;

    private static readonly Color WaterColor = new(0.25f, 0.85f, 1f, 0.55f);

    public override string DisplayName => "アクア・ブラスター";
    public override string Description =>
        "夏祭りの水鉄砲を魔改造した一品。当てた相手をずぶ濡れにし、暫くSinkHoleへ引きずり込む。";

    protected override string UniqueKey => "AquaBlaster";
    protected override ItemType BaseItem => ItemType.GunCOM18;

    protected override float   Damage       => 12f;
    protected override byte    MagazineSize => 30;
    protected override Vector3 Scale        => new(1f, 1f, 1.05f);

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => WaterColor;

    protected override void OnHurtingOthers(HurtingEventArgs ev)
    {
        if (ev.Player != null)
        {
            ev.Player.EnableEffect(EffectType.SinkHole, SinkHoleIntensity, SinkHoleDuration);
            ev.Player.EnableEffect(EffectType.Slowness, SlownessIntensity, SlownessDuration);
        }

        base.OnHurtingOthers(ev);
    }

    protected override void OnShot(ShotEventArgs ev)
    {
        base.OnShot(ev);
        SpawnWaterTrail(ev.Player, ev.Position);
    }

    private static void SpawnWaterTrail(Player? shooter, Vector3 impactPosition)
    {
        if (shooter?.CameraTransform == null) return;

        var origin = shooter.CameraTransform.position;

        for (int i = 1; i <= DropletCount; i++)
        {
            var position = Vector3.Lerp(origin, impactPosition, i / (float)DropletCount);

            try
            {
                var droplet = Primitive.Create(
                    PrimitiveType.Sphere, position, Vector3.zero, Vector3.one * 0.12f, true, WaterColor);
                droplet.Collidable = false;

                Timing.CallDelayed(DropletLifetime, () =>
                {
                    try { droplet.Destroy(); }
                    catch { /* ignored */ }
                });
            }
            catch (Exception ex)
            {
                Log.Error($"AquaBlaster water trail spawn failed: {ex}");
            }
        }
    }
}
