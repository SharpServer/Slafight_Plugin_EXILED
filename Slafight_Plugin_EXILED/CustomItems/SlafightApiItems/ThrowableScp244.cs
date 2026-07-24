#nullable enable
using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features.Doors;
using Exiled.API.Features.Pickups;
using Exiled.API.Features.Pickups.Projectiles;
using Exiled.Events.EventArgs.Map;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.Handlers;
using InventorySystem.Items.Usables.Scp244;
using MEC;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class ThrowableScp244 : CItem
{
    public override string DisplayName => "SCP-244 (投擲)";
    public override string Description => "投擲して使用することができるSCP-244";
    protected override string UniqueKey => "Scp244-Throwable";
    protected override ItemType BaseItem => ItemType.GrenadeFlash;
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.yellow;

    private static readonly Dictionary<Projectile, Scp244Pickup>? TrackedPickups = new();
    private static readonly Dictionary<Projectile, CoroutineHandle> TrackedCoroutines = new();

    public override void RegisterEvents()
    {
        Player.ThrownProjectile += OnThrown;
        Map.ExplodingGrenade += OnExploding;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Player.ThrownProjectile -= OnThrown;
        Map.ExplodingGrenade -= OnExploding;
        ClearTrackedProjectiles(destroyPickups: true);
        base.UnregisterEvents();
    }

    protected override void OnWaitingForPlayers()
    {
        ClearTrackedProjectiles(destroyPickups: true);

        var armoryDoor = Door.Get(DoorType.HczArmory);
        if (armoryDoor == null) return;

        Spawn(armoryDoor.Position + Vector3.up * 0.25f);

        base.OnWaitingForPlayers();
    }


    private void OnThrown(ThrownProjectileEventArgs ev)
    {
        if (!Check(ev.Projectile)) return;

        // Projectile前方にオフセットでスポーン（位置ずれ防止）
        Vector3 spawnPos = ev.Projectile.Position + ev.Projectile.Transform.forward * 0.5f;
        Pickup? bp;
        bp = Pickup.CreateAndSpawn(Random.Range(0, 2) is 0 ? ItemType.SCP244a : ItemType.SCP244b, spawnPos, ev.Projectile.Transform.rotation);

        // 親設定（Hierarchy整理用）
        bp.Transform.SetParent(ev.Projectile.Transform);
        bp.Transform.localPosition = Vector3.zero;
        bp.Transform.localRotation = Quaternion.identity;
        bp.Rigidbody.isKinematic = true;

        var scp244Pickup = bp.Cast<Scp244Pickup>();
        TrackedPickups?[ev.Projectile] = scp244Pickup;

        // コルーチン開始（追従）
        var coroutineHandle = Timing.RunCoroutine(TrackPickup(ev.Projectile, scp244Pickup));
        TrackedCoroutines[ev.Projectile] = coroutineHandle;
    }

    private static IEnumerator<float> TrackPickup(Projectile? projectile, Scp244Pickup? pickup)
    {
        while (projectile != null && pickup != null && pickup.GameObject != null)
        {
            // 毎フレーム位置/回転同期（Lerp/Slerpで滑らか追従）
            pickup.Position = Vector3.Lerp(pickup.Position, projectile.Position, Time.deltaTime * 60f);
            pickup.Rotation = Quaternion.Slerp(pickup.Rotation, projectile.Rotation, Time.deltaTime * 60f);

            yield return Timing.WaitForOneFrame;
        }
    }

    private void OnExploding(ExplodingGrenadeEventArgs ev)
    {
        if (TrackedPickups == null || !TrackedPickups.ContainsKey(ev.Projectile)) return;
        if (!Check(ev.Projectile)) return;

        var scp244 = TrackedPickups[ev.Projectile];

        // コルーチン停止
        if (TrackedCoroutines.TryGetValue(ev.Projectile, out var handle))
        {
            Timing.KillCoroutines(handle);
            TrackedCoroutines.Remove(ev.Projectile);
        }

        // 親解除 + 物理再有効
        scp244.Transform.parent = null;
        scp244.Position = ev.Projectile.Position;  // 最終同期
        scp244.Rigidbody.isKinematic = false;

        ev.TargetsToAffect.Clear();
        scp244.State = Scp244State.Active;
        Timing.CallDelayed(5f, () => scp244?.State = Scp244State.Destroyed);

        // クリーンアップ
        TrackedPickups.Remove(ev.Projectile);
    }

    private static void ClearTrackedProjectiles(bool destroyPickups)
    {
        foreach (var handle in TrackedCoroutines.Values)
            Timing.KillCoroutines(handle);

        TrackedCoroutines.Clear();

        if (destroyPickups && TrackedPickups != null)
        {
            foreach (var pickup in TrackedPickups.Values)
            {
                try { pickup?.Destroy(); }
                catch { /* ignored */ }
            }
        }

        TrackedPickups?.Clear();
    }
}
