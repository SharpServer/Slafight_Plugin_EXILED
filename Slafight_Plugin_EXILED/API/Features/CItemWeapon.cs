#nullable enable
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
/// CItem の Firearm 特化派生。Exiled.CustomItems.CustomWeapon 互換の
/// Damage / MagazineSize / Scale を virtual で受け取り、Spawn / Give 双方の経路で
/// Firearm へ焼き付ける。
/// </summary>
/// <remarks>
/// <para>Spawn は <see cref="Item.Create"/> 経由で Firearm を作って MagazineSize を流し込み、
/// <see cref="Item.CreatePickup"/> で Pickup に変換した後に Scale を反映する。
/// Give は <see cref="Player.AddItem"/> の戻り Item に対して MagazineSize を上書きする。</para>
/// <para>Damage は <see cref="OnHurtingOthers"/> でヒット量を上書きし、
/// 連射時のリロード弾薬計算は CustomWeapon 同等の動作を OnReloading / OnReloaded で再現する。</para>
/// </remarks>
public abstract class CItemWeapon : CItem
{
    /// <summary>1 撃あたりのダメージ。負値ならバニラのダメージを使う (override 無し)。</summary>
    protected virtual float Damage => -1f;

    /// <summary>マガジン容量。0 なら override 無し (バニラ MagazineSize)。</summary>
    protected virtual byte MagazineSize => 0;

    /// <summary>最大装弾数。0 なら override 無し。デフォルトでは MagazineSize と同じ値を使う。</summary>
    protected virtual ushort MaxMagazineAmmo => MagazineSize;

    /// <summary>生成 / 付与直後の装填数。0 なら override 無し。デフォルトでは MagazineSize と同じ値を使う。</summary>
    protected virtual ushort InitialMagazineAmmo => MagazineSize;

    /// <summary>1 発射あたりの消費弾数。0 なら override 無し。</summary>
    protected virtual byte AmmoDrain => 0;

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

        ApplyFirearmStats(firearm);
        ApplyFirearmAttachments(firearm);
    }

    /// <summary>最大装弾数 / 初期装填数 / 弾薬消費を Firearm に適用する。</summary>
    protected virtual void ApplyFirearmStats(Firearm firearm)
    {
        if (MaxMagazineAmmo > 0)
            firearm.MaxMagazineAmmo = MaxMagazineAmmo;

        if (InitialMagazineAmmo > 0)
            firearm.MagazineAmmo = InitialMagazineAmmo;

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

        if (MagazineSize > 0)
        {
            // CustomWeapon の弾薬計算ロジックを移植 (chamber 弾を考慮):
            // マガジン容量を MagazineSize に揃え、不足分はプレイヤーの所持弾から差し引く。
            // 自動銃の chamber に既に 1 発入っていれば実装上のマガジン上限はその分減る。
            var firearm = ev.Firearm;
            var ammoType = firearm.AmmoType;
            int magazineAmmo = firearm.MagazineAmmo;
            int chambered = firearm.Base.Modules
                .OfType<AutomaticActionModule>()
                .FirstOrDefault()?.SyncAmmoChambered ?? 0;
            int loadable = MagazineSize - chambered;
            int delta = -(MagazineSize - magazineAmmo - chambered);
            int available = ev.Player.GetAmmo(ammoType) + magazineAmmo;

            if (loadable < available)
            {
                firearm.MagazineAmmo = (byte)loadable;
                int remainder = ev.Player.GetAmmo(ammoType) + delta;
                ev.Player.SetAmmo(ammoType, (ushort)remainder);
            }
            else
            {
                firearm.MagazineAmmo = (byte)available;
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
        => MagazineSize > 0 && ev.Firearm.TotalAmmo >= MagazineSize;
}
