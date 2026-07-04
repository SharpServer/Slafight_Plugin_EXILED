using System;
using Exiled.API.Features;
using Exiled.API.Features.Toys;
using MEC;
using PlayerRoles.FirstPersonControl;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Abilities;

/// <summary>
/// WaterWarrior 特有アビリティその1。足元に水を噴射して高くジャンプする。
/// </summary>
public class AquaJumpAbility : AbilityBase
{
    protected override float DefaultCooldown => 12f;
    protected override int DefaultMaxUses => -1;

    private const float JumpPower    = 7.5f;
    private const float ForwardBoost = 3.5f;
    private const int JetCount       = 6;
    private const float JetLifetime  = 0.35f;
    private const float JetSpread    = 0.3f;

    private static readonly Color WaterColor = new(0.25f, 0.85f, 1f, 0.5f);

    protected override void ExecuteAbility(Player player)
    {
        try
        {
            SpawnWaterJet(player);
            Launch(player);
        }
        catch (Exception ex)
        {
            Log.Error($"AquaJumpAbility failed: {ex}");
        }
    }

    private static void Launch(Player player)
    {
        if (player.ReferenceHub?.roleManager?.CurrentRole is not IFpcRole fpcRole ||
            fpcRole.FpcModule?.ModuleReady != true)
            return;

        var forward = player.CameraTransform != null
            ? Vector3.ProjectOnPlane(player.CameraTransform.forward, Vector3.up).normalized
            : Vector3.zero;

        var current = fpcRole.FpcModule.Motor.Velocity;
        fpcRole.FpcModule.Motor.Velocity = new Vector3(
            current.x + forward.x * ForwardBoost,
            JumpPower,
            current.z + forward.z * ForwardBoost);

        fpcRole.FpcModule.Motor.ResetFallDamageCooldown();
    }

    private static void SpawnWaterJet(Player player)
    {
        var basePosition = player.Position + Vector3.down * 0.05f;

        for (int i = 0; i < JetCount; i++)
        {
            var offset = new Vector3(
                UnityEngine.Random.Range(-JetSpread, JetSpread),
                0f,
                UnityEngine.Random.Range(-JetSpread, JetSpread));

            try
            {
                var jet = Primitive.Create(
                    PrimitiveType.Cylinder, basePosition + offset, Vector3.zero,
                    new Vector3(0.15f, 0.6f, 0.15f), true, WaterColor);
                jet.Collidable = false;

                Timing.CallDelayed(JetLifetime, () =>
                {
                    try { jet.Destroy(); }
                    catch { /* ignored */ }
                });
            }
            catch (Exception ex)
            {
                Log.Error($"AquaJumpAbility water jet spawn failed: {ex}");
            }
        }
    }
}
