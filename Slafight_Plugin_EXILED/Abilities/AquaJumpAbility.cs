using System;
using Exiled.API.Features;
using Exiled.API.Features.Toys;
using MEC;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Abilities;

/// <summary>
/// WaterWarrior 特有アビリティその1。足元に水を噴射して高くジャンプする。
/// </summary>
public class AquaJumpAbility : AbilityBase
{
    protected override float DefaultCooldown => 12f;
    protected override int DefaultMaxUses => -1;

    private const float JumpPower       = 8.75f;
    private const float ForwardBoost    = 6f;
    private const float ForwardDuration = 0.2f;
    private const int JetCount          = 6;
    private const float JetLifetime     = 0.35f;
    private const float JetSpread       = 0.3f;

    private static readonly Color WaterColor = new(0.25f, 0.85f, 1f, 0.5f);
    private static readonly Vector3 JetGravity = new(0f, -4.5f, 0f);

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
        var forward = player.CameraTransform != null
            ? Vector3.ProjectOnPlane(player.CameraTransform.forward, Vector3.up).normalized
            : Vector3.zero;

        var horizontalVelocity = forward * ForwardBoost;

        if (!player.ForceFpcJump(JumpPower, horizontalVelocity, JetGravity, JetLifetime))
            return;

        player.ApplyFpcImpulse(horizontalVelocity, ForwardDuration);
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
