using System;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Toys;
using MEC;
using PlayerRoles.FirstPersonControl;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Abilities;

/// <summary>
/// WaterWarrior 特有アビリティその2。周囲に水しぶきを撒き散らし、近くの敵を吹き飛ばしつつSinkHoleを付与する。
/// </summary>
public class AquaSplashAbility : AbilityBase
{
    protected override float DefaultCooldown => 25f;
    protected override int DefaultMaxUses => -1;

    private const float Radius            = 6f;
    private const float KnockbackPower     = 6f;
    private const float UpwardPower        = 3f;
    private const byte SinkHoleIntensity   = 40;
    private const float SinkHoleDuration   = 8f;
    private const int RingSegments         = 10;
    private const float RingLifetime       = 0.4f;

    private static readonly Color WaterColor = new(0.25f, 0.85f, 1f, 0.45f);

    protected override void ExecuteAbility(Player player)
    {
        try
        {
            SpawnSplashRing(player.Position);
            HitNearbyEnemies(player);
        }
        catch (Exception ex)
        {
            Log.Error($"AquaSplashAbility failed: {ex}");
        }
    }

    private static void HitNearbyEnemies(Player player)
    {
        var selfTeam = player.GetTeam();

        foreach (var target in Player.List)
        {
            if (target?.ReferenceHub == null || target == player || !target.IsAlive) continue;
            if (target.GetTeam() == selfTeam) continue;
            if (Vector3.Distance(player.Position, target.Position) > Radius) continue;

            target.EnableEffect(EffectType.SinkHole, SinkHoleIntensity, SinkHoleDuration);
            Knockback(player, target);
        }
    }

    private static void Knockback(Player player, Player target)
    {
        if (target.ReferenceHub?.roleManager?.CurrentRole is not IFpcRole fpcRole ||
            fpcRole.FpcModule?.ModuleReady != true)
            return;

        var direction = target.Position - player.Position;
        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.01f ? direction.normalized : UnityEngine.Random.insideUnitSphere.normalized;

        fpcRole.FpcModule.Motor.Velocity =
            new Vector3(direction.x * KnockbackPower, UpwardPower, direction.z * KnockbackPower);
    }

    private static void SpawnSplashRing(Vector3 center)
    {
        for (int i = 0; i < RingSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / RingSegments;
            var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (Radius * 0.6f);
            var position = center + offset + Vector3.up * 0.1f;

            try
            {
                var droplet = Primitive.Create(
                    PrimitiveType.Sphere, position, Vector3.zero, Vector3.one * 0.25f, true, WaterColor);
                droplet.Collidable = false;

                Timing.CallDelayed(RingLifetime, () =>
                {
                    try { droplet.Destroy(); }
                    catch { /* ignored */ }
                });
            }
            catch (Exception ex)
            {
                Log.Error($"AquaSplashAbility ring spawn failed: {ex}");
            }
        }
    }
}
