#nullable enable
using System.Collections.Generic;
using Exiled.API.Features;
using MEC;
using Mirror;
using PlayerRoles.FirstPersonControl;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Extensions;

public static class FpcMovementExtensions
{
    private static readonly Dictionary<int, CoroutineHandle> GravityRestoreHandles = new();
    private static readonly Dictionary<int, Vector3> OriginalGravity = new();
    private static readonly Dictionary<int, CoroutineHandle> ImpulseHandles = new();

    public static bool TryGetFpcMovement(
        this Player? player,
        out FirstPersonMovementModule module,
        out FpcMotor motor)
    {
        module = null!;
        motor = null!;

        if (player?.ReferenceHub?.roleManager?.CurrentRole is not IFpcRole fpcRole ||
            fpcRole.FpcModule?.ModuleReady != true ||
            fpcRole.FpcModule.Motor == null)
            return false;

        module = fpcRole.FpcModule;
        motor = module.Motor;
        return true;
    }

    public static bool ForceFpcJump(
        this Player? player,
        float jumpPower,
        Vector3 horizontalVelocity,
        Vector3 temporaryGravity,
        float gravityDuration)
    {
        if (!player.TryGetFpcMovement(out _, out var motor))
            return false;

        if (gravityDuration > 0f)
            ApplyTemporaryGravity(player!, motor, temporaryGravity, gravityDuration);

        var current = motor.MoveDirection;
        motor.MoveDirection = new Vector3(horizontalVelocity.x, Mathf.Max(current.y, 0f), horizontalVelocity.z);
        motor.ResetFallDamageCooldown();

        motor.JumpController.ForceJump(jumpPower);

        var launched = motor.MoveDirection;
        motor.MoveDirection = new Vector3(horizontalVelocity.x, Mathf.Max(launched.y, jumpPower), horizontalVelocity.z);
        motor.ResetFallDamageCooldown();
        return true;
    }

    public static bool ApplyFpcImpulse(this Player? player, Vector3 impulseVelocity, float duration)
    {
        if (!player.TryGetFpcMovement(out _, out var motor))
            return false;

        if (impulseVelocity.sqrMagnitude < 0.01f)
            return false;

        if (impulseVelocity.y > 0f)
            motor.JumpController.ForceJump(impulseVelocity.y);

        if (ImpulseHandles.TryGetValue(player!.Id, out var previous))
            Timing.KillCoroutines(previous);

        ImpulseHandles[player.Id] = Timing.RunCoroutine(ImpulseCoroutine(player.Id, impulseVelocity, Mathf.Max(0.05f, duration)));
        return true;
    }

    private static void ApplyTemporaryGravity(Player player, FpcMotor motor, Vector3 temporaryGravity, float duration)
    {
        if (!OriginalGravity.ContainsKey(player.Id))
            OriginalGravity[player.Id] = motor.GravityController.Gravity;

        motor.GravityController.Gravity = temporaryGravity;

        if (GravityRestoreHandles.TryGetValue(player.Id, out var previous))
            Timing.KillCoroutines(previous);

        GravityRestoreHandles[player.Id] = Timing.RunCoroutine(RestoreGravityAfter(player.Id, player.ReferenceHub, duration));
    }

    private static IEnumerator<float> RestoreGravityAfter(int playerId, ReferenceHub hub, float delay)
    {
        yield return Timing.WaitForSeconds(delay);

        if (OriginalGravity.TryGetValue(playerId, out var originalGravity) && hub != null)
            FpcGravityController.ServerSetGravity(hub, originalGravity);

        OriginalGravity.Remove(playerId);
        GravityRestoreHandles.Remove(playerId);
    }

    private static IEnumerator<float> ImpulseCoroutine(int playerId, Vector3 velocity, float duration)
    {
        var elapsed = 0f;

        while (elapsed < duration)
        {
            var player = Player.Get(playerId);
            if (!player.TryGetFpcMovement(out var module, out var motor) || !player!.IsAlive)
                break;

            var deltaTime = Mathf.Clamp(Time.deltaTime, 0.01f, 0.05f);
            motor.MoveDirection = velocity;
            motor.ResetFallDamageCooldown();
            MoveAndSync(module, motor, velocity * deltaTime);

            elapsed += deltaTime;
            var decay = Mathf.Clamp01(deltaTime * 5.5f);
            velocity.x = Mathf.Lerp(velocity.x, 0f, decay);
            velocity.z = Mathf.Lerp(velocity.z, 0f, decay);
            velocity.y += motor.GravityController.Gravity.y * deltaTime * 0.25f;
            velocity.y = Mathf.Max(velocity.y, -1f);

            yield return Timing.WaitForOneFrame;
        }

        ImpulseHandles.Remove(playerId);
    }

    private static void MoveAndSync(FirstPersonMovementModule module, FpcMotor motor, Vector3 displacement)
    {
        if (module.CharControllerSet && module.CharController != null)
        {
            module.CharController.Move(displacement);
            module.Position = motor.CachedTransform.position;
        }
        else
        {
            module.Position += displacement;
        }

        if (NetworkServer.active && module.OnServerPositionOverwritten != null)
            module.ServerOverridePosition(module.Position);
    }
}
