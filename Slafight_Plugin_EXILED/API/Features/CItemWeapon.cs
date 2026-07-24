#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Item;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.Handlers;
using InventorySystem.Items.Firearms.Attachments;
using InventorySystem.Items.Firearms.Modules;
using UnityEngine;
using Item = Exiled.API.Features.Items.Item;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// CItem の Firearm 特化派生。Damage / MagazineSize / Scale を virtual で受け取り、
/// Spawn / Give 双方の経路で Firearm へ焼き付ける。
/// </summary>
/// <remarks>
/// <para>
/// MagazineSize / MaxMagazineAmmo は「アタッチメント補正後の実効容量」として扱う。
/// EXILED の MaxMagazineAmmo setter は内部の base capacity を変更するため、ここで
/// attachment modifier を逆算して、派生側が見た目通りの容量を指定できるようにする。
/// </para>
/// <para>
/// 弾薬状態（マガジン / バレル）は <see cref="OnCaptureState"/> / <see cref="OnRestoreState"/>
/// 経由でスナップショット・復元できる。CItemHybrid のモード切替で、物理アイテムを差し替えても
/// モードごとの弾数が保持される。
/// </para>
/// <para>
/// リロードは <see cref="ReloadAmmoMultiplier"/>（装填1発あたりの予備弾消費倍率）に基づき
/// 決定論的に処理する。発射前にマガジン/予備弾をスナップショットし、リロード完了時に
/// ネイティブ結果を上書きするため、アタッチメント補正に左右されず安定する。
/// </para>
/// </remarks>
public abstract class CItemWeapon : CItem
{
    // 発射前の TotalAmmo（AmmoDrain > 1 のときの手動消費計算用）
    private readonly Dictionary<ushort, int> _totalAmmoBeforeShot = new();

    // リロード開始時のスナップショット: serial → (マガジン弾, 予備弾)
    private readonly Dictionary<ushort, (int Magazine, int Reserve)> _reloadSnapshot = new();

    /// <summary>1 撃あたりのダメージ。負値ならバニラのダメージを使う (override 無し)。</summary>
    protected virtual float Damage => -1f;

    /// <summary>実効マガジン容量。0 なら override 無し (バニラ容量)。</summary>
    protected virtual byte MagazineSize => 0;

    /// <summary>実効最大装弾数。0 なら override 無し。デフォルトでは MagazineSize と同じ値を使う。</summary>
    protected virtual ushort MaxMagazineAmmo => MagazineSize;

    /// <summary>生成 / 付与直後の装填数。0 なら override 無し。デフォルトでは MaxMagazineAmmo と同じ値を使う。</summary>
    protected virtual ushort InitialMagazineAmmo => MaxMagazineAmmo;

    /// <summary>1 発射あたりの消費弾数。0 なら override 無し。</summary>
    protected virtual byte AmmoDrain => 0;

    /// <summary>
    /// リロードでマガジンに1発装填するごとに消費する予備弾の倍率。1 = バニラ等価。
    /// 例: 2 にすると 1 発込めるごとに予備弾を 2 発消費する（重い弾を表現）。
    /// </summary>
    protected virtual int ReloadAmmoMultiplier => 1;

    /// <summary>AmmoDrain 分の総弾数を持っていない場合に発射を止めるか。</summary>
    protected virtual bool RequireAmmoDrainAvailableToShoot => AmmoDrain > 1;

    /// <summary>Pickup の見た目スケール。Vector3.one ならバニラサイズ。</summary>
    protected virtual Vector3 Scale => Vector3.one;

    /// <summary>固定したいアタッチメント一覧。空ならアタッチメントは変更しない。</summary>
    protected virtual AttachmentName[] Attachments => [];

    /// <summary>Attachments 適用前に既存アタッチメントを消すか。</summary>
    protected virtual bool ClearAttachmentsBeforeApplying => Attachments.Length > 0;

    /// <summary>プレイヤーによるアタッチメント変更を許可するか。</summary>
    protected virtual bool AllowAttachmentChanges => true;

    /// <summary>基底側でリロード弾薬処理を行うか（容量上書き or 倍率カスタム時に有効）。</summary>
    private bool ManagesReloadAmmo => MaxMagazineAmmo > 0 || ReloadAmmoMultiplier > 1;

    // ==== Spawn / Give 経路: Item を作って MagazineSize / Scale を焼き付ける ====

    protected override Pickup? CreatePickupForSpawn(Vector3 position)
    {
        var item = Item.Create(BaseItem);
        if (item == null) return null;

        ApplyFirearmCustomization(item);
        var pickup = item.CreatePickup(position);
        if (pickup == null)
            return null;

        if (Scale != Vector3.one)
            pickup.Scale = Scale;

        return pickup;
    }

    protected override void CustomizeItem(Item item)
    {
        ApplyFirearmCustomization(item);
        base.CustomizeItem(item);
    }

    /// <summary>
    /// Item ベースで適用できる Firearm カスタマイズ。
    /// さらに特殊な処理が必要な場合だけ override する。
    /// </summary>
    protected virtual void ApplyFirearmCustomization(Item item)
    {
        if (item is not Firearm firearm)
            return;

        ApplyFirearmAttachments(firearm);
        ApplyFirearmStats(firearm);
    }

    /// <summary>最大装弾数 / 初期装填数 / 弾薬消費を Firearm に適用する。</summary>
    protected virtual void ApplyFirearmStats(Firearm firearm)
    {
        int effectiveMax = MaxMagazineAmmo;
        if (effectiveMax > 0)
            ApplyEffectiveMaxMagazineAmmo(firearm, effectiveMax);

        if (InitialMagazineAmmo > 0)
        {
            int initialAmmo = effectiveMax > 0
                ? Math.Min(InitialMagazineAmmo, effectiveMax)
                : InitialMagazineAmmo;
            firearm.MagazineAmmo = initialAmmo;
        }

        if (AmmoDrain > 0)
            firearm.AmmoDrain = AmmoDrain;
    }

    /// <summary>アタッチメント設定を Firearm に適用する。</summary>
    protected virtual void ApplyFirearmAttachments(Firearm firearm)
    {
        var attachments = Attachments;
        if (attachments.Length == 0)
            return;

        if (ClearAttachmentsBeforeApplying)
            firearm.ClearAttachments();

        foreach (var attachment in attachments)
            firearm.AddAttachment(attachment);
    }

    // ==== 状態キャプチャ / 復元（CItemHybrid のモード切替で使用） ====

    /// <summary>
    /// マガジン側合計弾数（マガジン弾 + 薬室弾）/ バレル弾のスナップショット。
    /// 薬室（chamber）弾を合算して保持するのは、復元後の <see cref="OnModeActivated"/> で
    /// ボルトをサイクルさせると 1 発が薬室へ移るため、合計で持っておかないと切替ごとに
    /// 薬室分の 1 発を失うため。
    /// </summary>
    private sealed record WeaponAmmoState(int MagazineTotal, int Barrel);

    protected override object? OnCaptureState(Item item)
    {
        if (item is not Firearm firearm)
            return null;

        return new WeaponAmmoState(firearm.MagazineAmmo + GetChambered(firearm), firearm.BarrelAmmo);
    }

    protected override void OnRestoreState(Item item, object? state)
    {
        if (item is not Firearm firearm || state is not WeaponAmmoState ammo)
            return;

        firearm.MagazineAmmo = ammo.MagazineTotal;
        firearm.BarrelAmmo   = ammo.Barrel;
    }

    /// <summary>
    /// モード切替で手持ちになった直後。強制持ち替えだと自動火器が未 cock のまま残り
    /// 発射不能になるため、ボルトを 1 回サイクルさせて cock + 薬室装填 + 同期する。
    /// （ServerCycleAction はマガジンから 1 発を薬室へ移すので合計弾数は維持される。）
    /// </summary>
    protected override void OnModeActivated(Item item)
    {
        if (item is not Firearm firearm)
            return;

        GetAutomaticModule(firearm)?.ServerCycleAction();
    }

    // ==== ダメージ override ====

    protected override void OnHurtingOthers(HurtingEventArgs ev)
    {
        if (!ev.IsAllowed) return;
        if (Damage >= 0f)
            ev.Amount = Damage;
    }

    protected override void OnShooting(ShootingEventArgs ev)
    {
        if (!ev.IsAllowed) return;
        if (ShouldBlockShooting(ev))
        {
            ev.IsAllowed = false;
            return;
        }

        if (AmmoDrain > 1)
            _totalAmmoBeforeShot[ev.Firearm.Serial] = ev.Firearm.TotalAmmo;
    }

    protected override void OnShot(ShotEventArgs ev)
    {
        ApplyManualAmmoDrain(ev.Firearm);
    }

    // ==== リロード（決定論的な弾薬取り回し） ====

    public override void RegisterEvents()
    {
        Player.ReloadingWeapon += OnInternalReloading;
        Player.ReloadedWeapon  += OnInternalReloaded;
        Exiled.Events.Handlers.Item.ChangingAttachments += OnInternalChangingAttachments;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Player.ReloadingWeapon -= OnInternalReloading;
        Player.ReloadedWeapon  -= OnInternalReloaded;
        Exiled.Events.Handlers.Item.ChangingAttachments -= OnInternalChangingAttachments;
        base.UnregisterEvents();
    }

    private void OnInternalChangingAttachments(ChangingAttachmentsEventArgs ev)
    {
        if (AllowAttachmentChanges) return;
        if (!Check(ev.Item)) return;
        ev.IsAllowed = false;
    }

    private void OnInternalReloading(ReloadingWeaponEventArgs ev)
    {
        if (!Check(ev.Item)) return;

        if (ShouldBlockReloading(ev))
        {
            // これ以上装填できない（容量満タン or 倍率コスト分の予備弾不足）
            ev.IsAllowed = false;
            return;
        }

        if (ManagesReloadAmmo)
        {
            // ネイティブリロードが弾薬を動かす前にスナップショットしておく。
            _reloadSnapshot[ev.Firearm.Serial] =
                (ev.Firearm.MagazineAmmo, ev.Player.GetAmmo(ev.Firearm.AmmoType));
        }

        OnReloading(ev);
    }

    private void OnInternalReloaded(ReloadedWeaponEventArgs ev)
    {
        if (!Check(ev.Item)) return;

        // スナップショットがあれば決定論的に上書きする（ネイティブ結果は破棄）。
        if (_reloadSnapshot.Remove(ev.Firearm.Serial, out var snapshot))
        {
            var firearm   = ev.Firearm;
            var ammoType  = firearm.AmmoType;
            int effMax    = GetConfiguredMaxMagazineAmmo(firearm);
            int chambered = GetChambered(firearm);
            int multiplier = Math.Max(1, ReloadAmmoMultiplier);

            int preMag    = snapshot.Magazine;
            int reserve   = snapshot.Reserve;

            // チャンバー分を除いたマガジン装填可能数
            int loadable   = Math.Max(0, effMax - preMag - chambered);
            int affordable = reserve / multiplier;
            int loaded     = Math.Min(loadable, affordable);

            firearm.MagazineAmmo = preMag + loaded;
            ev.Player.SetAmmo(ammoType, (ushort)Math.Max(0, reserve - (loaded * multiplier)));
        }

        OnReloaded(ev);
    }

    /// <summary>派生がリロード開始タイミングをフックしたい場合用。</summary>
    protected virtual void OnReloading(ReloadingWeaponEventArgs ev) { }

    /// <summary>派生がリロード完了タイミングをフックしたい場合用。</summary>
    protected virtual void OnReloaded(ReloadedWeaponEventArgs ev) { }

    /// <summary>
    /// これ以上装填できないときリロードを止めるか。
    /// 容量満タン、または倍率コスト分の予備弾を持っていない場合に止める。
    /// </summary>
    protected virtual bool ShouldBlockReloading(ReloadingWeaponEventArgs ev)
    {
        if (!ManagesReloadAmmo) return false;

        var firearm    = ev.Firearm;
        int effMax     = GetConfiguredMaxMagazineAmmo(firearm);
        int chambered  = GetChambered(firearm);
        int loadable   = Math.Max(0, effMax - firearm.MagazineAmmo - chambered);
        int affordable = ev.Player.GetAmmo(firearm.AmmoType) / Math.Max(1, ReloadAmmoMultiplier);

        return Math.Min(loadable, affordable) <= 0;
    }

    /// <summary>発射を基底側で止めるか。</summary>
    protected virtual bool ShouldBlockShooting(ShootingEventArgs ev)
        => RequireAmmoDrainAvailableToShoot
           && AmmoDrain > 1
           && ev.Firearm.TotalAmmo < AmmoDrain;

    private void ApplyManualAmmoDrain(Firearm firearm)
    {
        if (AmmoDrain <= 1) return;
        if (!_totalAmmoBeforeShot.Remove(firearm.Serial, out int beforeShotTotal)) return;

        int consumedByGame = Math.Max(0, beforeShotTotal - firearm.TotalAmmo);
        int remainingDrain = AmmoDrain - consumedByGame;
        if (remainingDrain <= 0) return;

        DrainStoredAmmo(firearm, ref remainingDrain);
    }

    private static void DrainStoredAmmo(Firearm firearm, ref int amount)
    {
        if (amount <= 0) return;

        int magazineDrain = Math.Min(firearm.MagazineAmmo, amount);
        if (magazineDrain > 0)
        {
            firearm.MagazineAmmo -= magazineDrain;
            amount -= magazineDrain;
        }

        int barrelDrain = Math.Min(firearm.BarrelAmmo, amount);
        if (barrelDrain > 0)
        {
            firearm.BarrelAmmo -= barrelDrain;
            amount -= barrelDrain;
        }
    }

    private int GetConfiguredMaxMagazineAmmo(Firearm firearm)
        => MaxMagazineAmmo > 0 ? MaxMagazineAmmo : firearm.MaxMagazineAmmo;

    /// <summary>銃の AutomaticActionModule。無ければ null（リボルバー/ParticleDisruptor 等）。</summary>
    private static AutomaticActionModule? GetAutomaticModule(Firearm firearm)
        => firearm.Base.Modules.OfType<AutomaticActionModule>().FirstOrDefault();

    /// <summary>チャンバー（薬室）に装填済みの弾数。AutomaticActionModule が無ければ 0。</summary>
    private static int GetChambered(Firearm firearm)
        => GetAutomaticModule(firearm)?.AmmoStored ?? 0;

    private static void ApplyEffectiveMaxMagazineAmmo(Firearm firearm, int effectiveMax)
    {
        int attachmentModifier = GetMagazineCapacityModifier(firearm);
        int baseCapacity = Math.Max(0, effectiveMax - attachmentModifier);
        firearm.MaxMagazineAmmo = baseCapacity;
    }

    private static int GetMagazineCapacityModifier(Firearm firearm)
    {
        try
        {
            return (int)firearm.Base.AttachmentsValue(AttachmentParam.MagazineCapacityModifier);
        }
        catch
        {
            return 0;
        }
    }
}
