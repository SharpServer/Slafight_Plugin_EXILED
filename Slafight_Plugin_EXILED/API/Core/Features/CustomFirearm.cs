using System;
using System.Collections.Generic;
using Exiled.API.Features.Items;
using InventorySystem.Items.Firearms.Attachments;
using LabApi.Events.Arguments.PlayerEvents;
using UnityEngine;

using ExiledItem = Exiled.API.Features.Items.Item;
using ExiledPickup = Exiled.API.Features.Pickups.Pickup;
using FirearmPickup = Exiled.API.Features.Pickups.FirearmPickup;
using LabPickup = LabApi.Features.Wrappers.Pickup;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// <see cref="CustomItem"/> の銃器向け基底です。
/// 値を override するだけで、付与・地面生成・再拾得の全経路へ同じ設定を適用します。
/// </summary>
public abstract class CustomFirearm : CustomItem
{
    private static readonly IReadOnlyList<AttachmentName> NoAttachments = Array.Empty<AttachmentName>();

    /// <summary>基礎ダメージ。null ならバニラ値を維持します。</summary>
    protected virtual float? Damage => null;

    /// <summary>基礎精度。null ならバニラ値を維持します。</summary>
    protected virtual float? Inaccuracy => null;

    /// <summary>基礎貫通力。null ならバニラ値を維持します。</summary>
    protected virtual float? Penetration => null;

    /// <summary>ダメージ減衰距離。null ならバニラ値を維持します。</summary>
    protected virtual float? DamageFalloffDistance => null;

    /// <summary>実効マガジン容量。null ならバニラ値を維持します。</summary>
    protected virtual int? MagazineCapacity => null;

    /// <summary>生成直後の装填数。null なら容量またはバニラ値を使います。</summary>
    protected virtual int? InitialMagazineAmmo => MagazineCapacity;

    /// <summary>1 発射あたりの消費弾数。</summary>
    protected virtual int AmmoDrain => 1;

    /// <summary>固定するアタッチメント。空なら現在値を維持します。</summary>
    protected virtual IReadOnlyList<AttachmentName> Attachments => NoAttachments;

    /// <summary>指定アタッチメントを適用する前に既定構成へ戻すか。</summary>
    protected virtual bool ClearAttachmentsBeforeApplying => Attachments.Count > 0;

    /// <summary>プレイヤーによるアタッチメント変更を許可するか。</summary>
    protected virtual bool AllowAttachmentChanges => true;

    /// <summary>Pickup の見た目の大きさ。</summary>
    protected virtual Vector3 PickupScale => Vector3.one;

    protected override void Customize(LabApi.Features.Wrappers.Item item)
    {
        if (ExiledItem.Get(item.Base) is Firearm firearm)
            Apply(firearm);

        base.Customize(item);
    }

    protected override void CustomizeNewItem(LabApi.Features.Wrappers.Item item)
    {
        // 永続設定は通常の Customize と同じ経路を通し、弾数だけを新規生成時に初期化する。
        // これにより派生型の Customize も初回から呼ばれ、再拾得時に残弾が補充されない。
        Customize(item);

        if (ExiledItem.Get(item.Base) is Firearm firearm)
            Initialize(firearm);
    }

    protected override void Customize(LabPickup pickup)
    {
        if (ExiledPickup.Get(pickup.Base) is FirearmPickup firearmPickup)
            Apply(firearmPickup);

        pickup.Transform.localScale = Vector3.Scale(pickup.Transform.localScale, PickupScale);
        base.Customize(pickup);
    }

    protected override LabPickup CreatePickup(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (ExiledItem.Create(BaseType) is not Firearm firearm)
            return base.CreatePickup(position, rotation, Vector3.Scale(scale, PickupScale));

        Apply(firearm);
        Initialize(firearm);
        ExiledPickup pickup = firearm.CreatePickup(position, rotation, spawn: false);
        if (pickup is null)
            return null;

        pickup.Scale = scale;
        return LabPickup.Get(pickup.Base);
    }

    protected override void OnAttachmentsChanging(PlayerChangingAttachmentsEventArgs ev)
    {
        if (!AllowAttachmentChanges)
            ev.IsAllowed = false;

        base.OnAttachmentsChanging(ev);
    }

    /// <summary>インベントリ内の銃へ宣言値を適用します。</summary>
    protected virtual void Apply(Firearm firearm)
    {
        if (Damage is { } damage)
            firearm.Damage = damage;
        if (Inaccuracy is { } inaccuracy)
            firearm.Inaccuracy = inaccuracy;
        if (Penetration is { } penetration)
            firearm.Penetration = penetration;
        if (DamageFalloffDistance is { } falloff)
            firearm.DamageFalloffDistance = falloff;
        if (MagazineCapacity is { } capacity)
            firearm.MaxMagazineAmmo = Math.Max(0, capacity);

        firearm.AmmoDrain = Math.Max(1, AmmoDrain);

        if (Attachments.Count == 0)
            return;

        if (ClearAttachmentsBeforeApplying)
            firearm.ClearAttachments();
        firearm.AddAttachment(Attachments);
    }

    /// <summary>新規作成した銃だけに初期状態を適用します。</summary>
    protected virtual void Initialize(Firearm firearm)
    {
        if (InitialMagazineAmmo is not { } initial)
            return;

        int maximum = MagazineCapacity ?? firearm.MaxMagazineAmmo;
        firearm.MagazineAmmo = Math.Max(0, Math.Min(initial, maximum));
    }

    /// <summary>地面の銃へ、Pickup が保持できる宣言値を適用します。</summary>
    protected virtual void Apply(FirearmPickup pickup)
    {
        if (Damage is { } damage)
            pickup.Damage = damage;
        if (Inaccuracy is { } inaccuracy)
            pickup.Inaccuracy = inaccuracy;
        if (Penetration is { } penetration)
            pickup.Penetration = penetration;
        if (DamageFalloffDistance is { } falloff)
            pickup.DamageFalloffDistance = falloff;
        if (MagazineCapacity is { } capacity)
            pickup.MaxAmmo = Math.Max(0, capacity);
        pickup.AmmoDrain = Math.Max(1, AmmoDrain);
    }
}
