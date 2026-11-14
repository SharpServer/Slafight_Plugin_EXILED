using System;
using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using LabApi.Events.Arguments.PlayerEvents;
using MEC;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Core.Enums;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.Weapons;

/// <summary>
/// 投げる操作で第五的な光弾を撃つカード型の武器です。撃つと 10 秒間チャージ中のカードに変わります。
/// </summary>
public sealed class Mindblaster : CustomHybrid
{
    private const float ChargeSeconds = 10f;

    private const float Range = 25f;

    /// <summary>射程端での威力の倍率です。</summary>
    private const float MinPower = 0.25f;

    private readonly ReadyCard ready;
    private readonly ChargingCard charging;

    private bool isCharging;
    private float chargeStartedAt;
    private CoroutineHandle chargeHandle;

    public Mindblaster()
    {
        ready = new ReadyCard(this);
        charging = new ChargingCard(this);
    }

    protected override bool EnableKeyModeSwitch => false;

    protected override bool ShowModeSwitchHint => false;

    protected override List<CustomHybridMode> BuildSubModes() =>
    [
        new(ready, "発射可能"),
        new(charging, "チャージ中"),
    ];

    /// <summary>
    /// <paramref name="start"/> から視線方向へ 0.8 秒で 25m 飛ぶ光弾を撃ちます。
    /// 光弾から 1m 以内に居る相手は、居続ける間フレームごとに被弾します。
    /// ダメージと効果時間は遠くまで飛ぶほど弱まり、射程端では <see cref="MinPower"/> 倍になります。
    /// 見た目も飛ぶほど薄れ、射程端で消えます。
    /// </summary>
    /// <returns>光弾を出せたら true。</returns>
    public static bool Launch(Player owner, Vector3 start, float damage, float effectDuration, Func<Player, bool> canHit)
    {
        Vector3 forward = owner.CameraTransform.forward;
        if (!SchematicSpawner.TrySpawnMovable("SCP3005", start, forward, out SchematicObject schematic))
        {
            Log.Error("[Mindblaster] 光弾の生成に失敗しました。");

            return false;
        }

        RoundScope.Current.Track(Timing.RunCoroutine(Fly(schematic, owner, forward.normalized, damage, effectDuration, canHit)));

        return true;
    }

    protected override void OnTrackingStopped()
    {
        Timing.KillCoroutines(chargeHandle);
        chargeHandle = default;
        isCharging = false;
        base.OnTrackingStopped();
    }

    private void Fire(PlayerDroppingItemEventArgs ev)
    {
        if (!ev.Throw) return;

        ev.IsAllowed = false;

        if (isCharging || Player.Get(ev.Player.ReferenceHub) is not { } player) return;

        if (!Launch(player, player.Position, 10f, 15f, _ => true))
        {
            player.ShowHint("<size=23>第五思壊線の発射に失敗しました。</size>", 3f);

            return;
        }

        isCharging = true;
        chargeStartedAt = Time.time;
        SwitchMode(ev.Item.Serial, player);

        if (!IsCurrentSub(Serial, charging))
        {
            isCharging = false;
            Log.Error($"[Mindblaster] チャージ中への切り替えに失敗しました (serial: {ev.Item.Serial})。");
            player.ShowHint("<size=23>第五思壊線のチャージ開始に失敗しました。</size>", 3f);

            return;
        }

        chargeHandle = RoundScope.Current.Track(Timing.RunCoroutine(Charge()));
        player.ShowHint($"<size=23>第五思壊線をチャージ中です（{ChargeSeconds:0.#}秒）。</size>", 3f);
    }

    private void BlockChargingThrow(PlayerDroppingItemEventArgs ev)
    {
        if (!ev.Throw) return;

        ev.IsAllowed = false;

        float remaining = Mathf.Max(0f, ChargeSeconds - (Time.time - chargeStartedAt));
        Player.Get(ev.Player.ReferenceHub)?.ShowHint($"<size=23>チャージ中です（残り {remaining:0.#}秒）。</size>", 2f);
    }

    private IEnumerator<float> Charge()
    {
        while (Time.time - chargeStartedAt < ChargeSeconds)
            yield return Timing.WaitForSeconds(0.1f);

        // 死亡で床に落ちた直後などは、次の tick で拾い直す。
        while (!TryCompleteCharge())
            yield return Timing.WaitForSeconds(0.1f);
    }

    private bool TryCompleteCharge()
    {
        if (Serial == 0)
            return true;

        if (Owner is { } owner)
        {
            Exiled.API.Features.Items.Item previous = owner.CurrentItem;
            bool wasHeld = previous?.Serial == Serial;

            SwitchMode(Serial, owner);

            if (!IsCurrentSub(Serial, ready))
                return false;

            // 別のものを持っていたなら、持ち替えさせない。
            if (!wasHeld && previous is not null && owner.Items.Any(item => item.Serial == previous.Serial))
                owner.CurrentItem = previous;

            isCharging = false;
            chargeHandle = default;
            owner.ShowHint("<size=23>第五思壊線のチャージが完了しました。\n残り発射回数: ∞</size>", 3f);

            return true;
        }

        if (Pickup is { } pickup)
        {
            // 床のカードは持ち替えられないので、発射可能な新品に置き換える。
            Vector3 position = pickup.Position;
            Quaternion rotation = pickup.Rotation;

            chargeHandle = default;
            Destroy();
            Spawn<Mindblaster>(position, rotation);

            return true;
        }

        return false;
    }

    private static IEnumerator<float> Fly(
        SchematicObject schematic,
        Player owner,
        Vector3 forward,
        float damage,
        float effectDuration,
        Func<Player, bool> canHit)
    {
        const float duration = 0.8f;

        if (schematic == null) yield break;

        SchematicVisual visual = new SchematicVisual(schematic);
        Vector3 start = schematic.transform.position;
        Vector3 end = start + forward * Range + new Vector3(0f, 0.15f, 0f);
        float elapsed = 0f;

        while (elapsed < duration && schematic != null && owner.IsSafePlayer())
        {
            float power = Mathf.Lerp(1f, MinPower, Mathf.Clamp01(elapsed / duration));
            float scaledEffect = effectDuration * power;

            foreach (Player target in Player.List)
            {
                if (!target.IsSafePlayer() || target == owner || !target.IsAlive || !canHit(target)) continue;
                if (Vector3.Distance(schematic.transform.position, target.Position) > 1f) continue;

                target.EnableEffect<Burned>(255, scaledEffect);
                target.EnableEffect<Concussed>(255, scaledEffect);
                target.EnableEffect<Asphyxiated>(1, scaledEffect);
                target.Hurt(owner, damage * power, DamageType.Unknown, null, $"<color={ServerColors.Magenta}>第五的</color>な力による影響");
                owner.ShowHitMarker();
            }

            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            schematic.transform.position = Vector3.Lerp(start, end, progress);
            visual.Fade(1f - progress);

            yield return 0f;
        }

        if (schematic != null)
            schematic.Destroy();
    }

    /// <summary>発射できる状態のカードです。</summary>
    private sealed class ReadyCard(Mindblaster owner) : CustomKeycard
    {
        public override string Name => "第五思壊線";
        public override string Description => $"非常に<color={ServerColors.Magenta}>第五的な光</color>を発射し思考を破壊する";
        protected override CustomKeycardType KeycardType => CustomKeycardType.TaskForce;
        protected override KeycardPermissions Permissions => KeycardPermissions.None;
        protected override string Label => "Mindblaster";
        protected override Color32? LabelColor => new(255, 0, 250, 255);
        protected override string HolderName => "Mgc. Fifth";
        protected override Color32? TintColor => new(255, 0, 250, 255);
        protected override Color32? PermissionsColor => new(255, 255, 255, 255);
        protected override byte? Rank => 1;
        protected override string SerialNumber => "555555555555";
        protected override bool PickupLightEnabled => true;
        protected override Color PickupLightColor => Color.magenta;

        protected override void OnDropStarting(PlayerDroppingItemEventArgs ev) => owner.Fire(ev);
    }

    /// <summary>チャージ中のカードです。投げても撃てません。</summary>
    private sealed class ChargingCard(Mindblaster owner) : CustomKeycard
    {
        public override string Name => "第五思壊線（チャージ中）";
        public override string Description => "第五思壊線をチャージしている";
        protected override CustomKeycardType KeycardType => CustomKeycardType.TaskForce;
        protected override KeycardPermissions Permissions => KeycardPermissions.None;
        protected override string Label => "CHARGING";
        protected override Color32? LabelColor => new(210, 210, 210, 255);
        protected override string HolderName => "Mindblaster";
        protected override Color32? TintColor => new(105, 105, 105, 255);
        protected override Color32? PermissionsColor => new(145, 145, 145, 255);
        protected override byte? Rank => 1;
        protected override string SerialNumber => "CHARGING";
        protected override bool PickupLightEnabled => false;

        protected override void OnDropStarting(PlayerDroppingItemEventArgs ev) => owner.BlockChargingThrow(ev);
    }
}
