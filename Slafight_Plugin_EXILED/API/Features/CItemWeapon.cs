#nullable enable
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Item;
using Exiled.Events.EventArgs.Player;
using InventorySystem.Items.Firearms.Attachments;
using InventorySystem.Items.Firearms.Modules;
using UnityEngine;

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
/// </remarks>
public abstract class CItemWeapon : CItem
{
    private readonly Dictionary<ushort, int> _totalAmmoBeforeShot = new();

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

    // ==== Spawn / Give 経路: Item を作って MagazineSize / Scale を焼き付ける ====

    protected override Pickup? CreatePickupForSpawn(Vector3 position)
    {
        var item = Item.Create(BaseItem);
        if (item == null) return null;

        ApplyFirearmCustomization(item);
        var pickup = item.CreatePickup(position);
        if (pickup != null && Scale != Vector3.one)
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
                ? System.Math.Min(InitialMagazineAmmo, effectiveMax)
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

    // ==== リロード時のマガジン取り回し (CustomWeapon 互換) ====

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Player.ReloadingWeapon += OnInternalReloading;
        Exiled.Events.Handlers.Player.ReloadedWeapon  += OnInternalReloaded;
        Exiled.Events.Handlers.Item.ChangingAttachments += OnInternalChangingAttachments;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Player.ReloadingWeapon -= OnInternalReloading;
        Exiled.Events.Handlers.Player.ReloadedWeapon  -= OnInternalReloaded;
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
            // 既に MagazineSize 分弾を抱えていればリロード不可。
            ev.IsAllowed = false;
            return;
        }

        OnReloading(ev);
    }

    private void OnInternalReloaded(ReloadedWeaponEventArgs ev)
    {
        if (!Check(ev.Item)) return;

        if (MaxMagazineAmmo > 0)
        {
            // CustomWeapon の弾薬計算ロジックを実効容量ベースで移植 (chamber 弾を考慮)。
            var firearm = ev.Firearm;
            var ammoType = firearm.AmmoType;
            int maxMagazineAmmo = GetConfiguredMaxMagazineAmmo(firearm);
            int magazineAmmo = firearm.MagazineAmmo;
            int chambered = firearm.Base.Modules
                .OfType<AutomaticActionModule>()
                .FirstOrDefault()?.SyncAmmoChambered ?? 0;
            int loadable = System.Math.Max(0, maxMagazineAmmo - chambered);
            int delta = -(maxMagazineAmmo - magazineAmmo - chambered);
            int available = ev.Player.GetAmmo(ammoType) + magazineAmmo;

            if (loadable < available)
            {
                firearm.MagazineAmmo = loadable;
                int remainder = ev.Player.GetAmmo(ammoType) + delta;
                ev.Player.SetAmmo(ammoType, (ushort)remainder);
            }
            else
            {
                firearm.MagazineAmmo = available;
                ev.Player.SetAmmo(ammoType, 0);
            }
        }

        OnReloaded(ev);
    }

    /// <summary>派生がリロード開始タイミングをフックしたい場合用。</summary>
    protected virtual void OnReloading(ReloadingWeaponEventArgs ev) { }

    /// <summary>派生がリロード完了タイミングをフックしたい場合用。</summary>
    protected virtual void OnReloaded(ReloadedWeaponEventArgs ev) { }

    /// <summary>MagazineSize 到達時に基底側でリロードを止めるか。</summary>
    protected virtual bool ShouldBlockReloading(ReloadingWeaponEventArgs ev)
        => MaxMagazineAmmo > 0 && ev.Firearm.TotalAmmo >= GetConfiguredMaxMagazineAmmo(ev.Firearm);

    /// <summary>発射を基底側で止めるか。</summary>
    protected virtual bool ShouldBlockShooting(ShootingEventArgs ev)
        => RequireAmmoDrainAvailableToShoot
           && AmmoDrain > 1
           && ev.Firearm.TotalAmmo < AmmoDrain;

    private void ApplyManualAmmoDrain(Firearm firearm)
    {
        if (AmmoDrain <= 1) return;
        if (!_totalAmmoBeforeShot.Remove(firearm.Serial, out int beforeShotTotal)) return;

        int consumedByGame = System.Math.Max(0, beforeShotTotal - firearm.TotalAmmo);
        int remainingDrain = AmmoDrain - consumedByGame;
        if (remainingDrain <= 0) return;

        DrainStoredAmmo(firearm, ref remainingDrain);
    }

    private static void DrainStoredAmmo(Firearm firearm, ref int amount)
    {
        if (amount <= 0) return;

        int magazineDrain = System.Math.Min(firearm.MagazineAmmo, amount);
        if (magazineDrain > 0)
        {
            firearm.MagazineAmmo -= magazineDrain;
            amount -= magazineDrain;
        }

        int barrelDrain = System.Math.Min(firearm.BarrelAmmo, amount);
        if (barrelDrain > 0)
        {
            firearm.BarrelAmmo -= barrelDrain;
            amount -= barrelDrain;
        }
    }

    private int GetConfiguredMaxMagazineAmmo(Firearm firearm)
        => MaxMagazineAmmo > 0 ? MaxMagazineAmmo : firearm.MaxMagazineAmmo;

    private static void ApplyEffectiveMaxMagazineAmmo(Firearm firearm, int effectiveMax)
    {
        int attachmentModifier = GetMagazineCapacityModifier(firearm);
        int baseCapacity = System.Math.Max(0, effectiveMax - attachmentModifier);
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
