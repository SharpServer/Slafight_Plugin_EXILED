using System;
using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Toys;
using Exiled.Events.EventArgs.Player;
using MEC;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

/// <summary>
/// WaterWarrior 特有武器その2。直撃した相手を吹き飛ばし、長時間SinkHoleへ引きずり込む高圧放水砲。
/// </summary>
public class HydroCannon : CItemWeapon
{
    private const byte SinkHoleIntensity  = 60;
    private const float SinkHoleDuration  = 14f;
    private const float KnockbackPower    = 12f;
    private const float UpwardPower       = 2.5f;
    private const float KnockbackDuration = 0.35f;
    private const float ShotMemorySeconds = 0.35f;
    private const int BurstCount          = 4;
    private const float BurstLifetime     = 0.3f;

    private static readonly Color WaterColor = new(0.2f, 0.75f, 1f, 0.5f);
    private readonly Dictionary<(int AttackerId, int TargetId), ShotImpulse> _recentShotImpulses = new();

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
        if (ev.IsAllowed && ev.Attacker != null && ev.Player != null &&
            ev.Attacker.GetTeam() != ev.Player.GetTeam())
        {
            ev.Player.EnableEffect(EffectType.SinkHole, SinkHoleIntensity, SinkHoleDuration);
            Knockback(ev.Attacker, ev.Player);
        }

        base.OnHurtingOthers(ev);
    }

    protected override void OnShot(ShotEventArgs ev)
    {
        base.OnShot(ev);
        RememberShotImpulse(ev);
        SpawnSplashBurst(ev.Position);
    }

    protected override void OnWaitingForPlayers()
    {
        _recentShotImpulses.Clear();
        base.OnWaitingForPlayers();
    }

    private void RememberShotImpulse(ShotEventArgs ev)
    {
        if (ev.Player == null || ev.Target == null || ev.Player.GetTeam() == ev.Target.GetTeam())
            return;

        var direction = GetShotDirection(ev);
        if (direction.sqrMagnitude < 0.01f)
            return;

        _recentShotImpulses[(ev.Player.Id, ev.Target.Id)] = new ShotImpulse(direction.normalized, Time.time + ShotMemorySeconds);
    }

    private void Knockback(Player attacker, Player target)
    {
        var direction = TryTakeShotDirection(attacker, target, out var shotDirection)
            ? shotDirection
            : GetFallbackDirection(attacker, target);

        var impulse = direction.normalized * KnockbackPower;
        impulse.y = Mathf.Max(impulse.y, UpwardPower);

        target.ApplyFpcImpulse(impulse, KnockbackDuration);
    }

    private bool TryTakeShotDirection(Player attacker, Player target, out Vector3 direction)
    {
        var key = (attacker.Id, target.Id);
        if (_recentShotImpulses.TryGetValue(key, out var impulse))
        {
            _recentShotImpulses.Remove(key);
            if (Time.time <= impulse.ExpiresAt)
            {
                direction = impulse.Direction;
                return true;
            }
        }

        direction = Vector3.zero;
        return false;
    }

    private static Vector3 GetShotDirection(ShotEventArgs ev)
    {
        if (ev.Player?.CameraTransform != null)
        {
            var direction = ev.Position - ev.Player.CameraTransform.position;
            if (direction.sqrMagnitude > 0.01f)
                return direction.normalized;

            return ev.Player.CameraTransform.forward.normalized;
        }

        return Vector3.forward;
    }

    private static Vector3 GetFallbackDirection(Player attacker, Player target)
    {
        if (attacker.CameraTransform != null)
        {
            var cameraDirection = attacker.CameraTransform.forward;
            if (cameraDirection.sqrMagnitude > 0.01f)
                return cameraDirection.normalized;
        }

        var direction = target.Position - attacker.Position;
        return direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.forward;
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

    private readonly struct ShotImpulse
    {
        public ShotImpulse(Vector3 direction, float expiresAt)
        {
            Direction = direction;
            ExpiresAt = expiresAt;
        }

        public Vector3 Direction { get; }
        public float ExpiresAt { get; }
    }
}
