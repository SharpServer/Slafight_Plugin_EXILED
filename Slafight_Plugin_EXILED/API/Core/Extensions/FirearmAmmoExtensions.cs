using System;
using System.Reflection;
using Exiled.API.Features.Items;
using Exiled.API.Features.Items.FirearmModules.Primary;
using Exiled.API.Features.Pickups;
using InventorySystem.Items.Firearms.Attachments;
using InventorySystem.Items.Firearms.Modules;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.API.Core.Extensions;

/// <summary>銃器と Pickup の弾薬設定を扱う拡張メソッドです。</summary>
public static class FirearmAmmoExtensions
{
    /// <summary>弾薬設定を銃へ適用します。固定アタッチメントを設定した後に呼んでください。</summary>
    public static void ApplyAmmoSettings(this Firearm firearm, FirearmAmmoSettings settings)
    {
        if (firearm is null || settings is null)
            return;

        if (settings.MagazineCapacity is { } capacity)
        {
            // EXILED's setter writes the base capacity, while the getter and native
            // reload validator include the attachment modifier (e.g. LowcapMagJHP).
            // Settings specify the final capacity, so compensate for that modifier.
            int modifier = firearm.PrimaryMagazine is NormalMagazine
                ? (int)firearm.Base.AttachmentsValue(AttachmentParam.MagazineCapacityModifier)
                : 0;
            firearm.MaxMagazineAmmo = Math.Max(0, capacity) - modifier;
        }

        // 固定アタッチメントが弾種を選んだ後に適用し、ReserveAmmo の ItemType と一致させる。
        if (settings.AmmoItemType is { } ammoItemType)
            firearm.SetAmmoItemType(ammoItemType);

        firearm.AmmoDrain = settings.AmmoDrain;

        if (settings.MagazineCapacity is not null && firearm.MagazineAmmo > firearm.MaxMagazineAmmo)
            firearm.MagazineAmmo = Math.Max(0, firearm.MaxMagazineAmmo);
    }

    /// <summary>Pickup に保存できる弾薬設定を適用します。</summary>
    public static void ApplyAmmoSettings(this FirearmPickup pickup, FirearmAmmoSettings settings)
    {
        if (pickup is null || settings is null)
            return;

        if (settings.MagazineCapacity is { } capacity)
        {
            // FirearmPickup.MaxAmmo is copied into the base capacity on acquisition.
            // Preserve the same attachment compensation as inventory firearms.
            int modifier = AttachmentPreview.TryGet(pickup.Type, pickup.Attachments, false, out var preview) &&
                preview.TryGetModule<MagazineModule>(out _)
                ? (int)preview.AttachmentsValue(AttachmentParam.MagazineCapacityModifier)
                : 0;
            pickup.MaxAmmo = Math.Max(0, capacity) - modifier;
        }

        pickup.AmmoDrain = settings.AmmoDrain;
    }

    /// <summary>
    /// 新規作成した銃を装填可能な状態にします。通常の Plugin AddItem 経路では
    /// マガジンが未挿入の場合があるため、初期弾数を書き込む前に挿入します。
    /// </summary>
    public static void InitializeAmmo(this Firearm firearm, FirearmAmmoSettings settings)
    {
        if (firearm is null || settings is null)
            return;

        if (firearm.PrimaryMagazine is NormalMagazine magazine && !magazine.MagazineInserted)
            magazine.InsertMagazine();

        if (settings.InitialMagazineAmmo is not { } initial)
            return;

        int maximum = Math.Max(0, firearm.MaxMagazineAmmo);
        firearm.MagazineAmmo = Math.Max(0, Math.Min(initial, maximum));

        // Ammo in the magazine alone does not make a closed-bolt firearm shootable.
        // Use the native action so EXILED's AmmoDrain is charged while chambering.
        firearm.PrepareForUse();
    }

    /// <summary>現在の弾倉・薬室・銃身状態を保存します。</summary>
    public static FirearmAmmoState CaptureAmmoState(this Firearm firearm)
    {
        if (firearm is null)
            return null;

        AutomaticActionModule automatic = GetAutomaticModule(firearm);
        bool magazineInserted = firearm.PrimaryMagazine is not NormalMagazine magazine || magazine.MagazineInserted;
        return new FirearmAmmoState(
            firearm.MagazineAmmo,
            automatic?.AmmoStored ?? 0,
            firearm.BarrelAmmo,
            magazineInserted);
    }

    /// <summary>保存した弾薬状態を容量内へ復元します。</summary>
    public static void RestoreAmmoState(this Firearm firearm, FirearmAmmoState state)
    {
        if (firearm is null || state is null)
            return;

        int maximum = Math.Max(0, firearm.MaxMagazineAmmo);
        firearm.MagazineAmmo = Math.Max(0, Math.Min(state.MagazineAmmo, maximum));

        // EXILED's MagazineAmmo setter always inserts a magazine. Restore insertion
        // after writing the ammo, otherwise a saved removed magazine becomes inserted.
        if (firearm.PrimaryMagazine is NormalMagazine magazine)
            magazine.MagazineInserted = state.MagazineInserted;

        firearm.BarrelAmmo = Math.Max(0, Math.Min(state.BarrelAmmo, firearm.MaxBarrelAmmo));

        if (GetAutomaticModule(firearm) is { } automatic)
        {
            automatic.AmmoStored = Math.Max(0, Math.Min(state.ChamberedAmmo, automatic.ChamberSize));
            automatic.ServerResync();
        }
    }

    /// <summary>モード切替後に自動火器を作動させ、薬室を使用可能な状態にします。</summary>
    public static void PrepareForUse(this Firearm firearm)
    {
        if (firearm is not null)
            GetAutomaticModule(firearm)?.ServerCycleAction();
    }

    /// <summary>
    /// 銃が消費する予備弾薬の ItemType を変更します。
    /// 通常弾薬以外の ItemType も指定でき、在庫は同じ ItemType の Ammo 枠から消費されます。
    /// </summary>
    public static void SetAmmoItemType(this Firearm firearm, ItemType ammoItemType)
    {
        if (firearm is null)
            throw new ArgumentNullException(nameof(firearm));
        if (ammoItemType == ItemType.None)
            throw new ArgumentOutOfRangeException(nameof(ammoItemType), ammoItemType, "Ammo item type cannot be None.");

        IPrimaryAmmoContainerModule primaryAmmo = Array.Find(
            firearm.Base.Modules,
            module => module is IPrimaryAmmoContainerModule) as IPrimaryAmmoContainerModule;

        if (primaryAmmo is null)
            throw new InvalidOperationException($"{firearm.Type} has no primary ammo container.");

        Type type = primaryAmmo.GetType();
        PropertyInfo ammoTypeProperty = type.GetProperty(
            "AmmoType",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (ammoTypeProperty?.CanWrite == true && ammoTypeProperty.PropertyType == typeof(ItemType))
        {
            ammoTypeProperty.SetValue(primaryAmmo, ammoItemType);
            return;
        }

        // MagazineModule exposes only a read-only AmmoType property; its serialized
        // ItemType field is the value consumed by ReloadAuthorization and reloading.
        for (Type declaringType = type; declaringType is not null; declaringType = declaringType.BaseType)
        {
            FieldInfo ammoTypeField = declaringType.GetField(
                "_ammoType",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

            if (ammoTypeField?.FieldType == typeof(ItemType))
            {
                ammoTypeField.SetValue(primaryAmmo, ammoItemType);
                return;
            }
        }

        throw new NotSupportedException($"Cannot set an ammo ItemType on {type.FullName}.");
    }

    private static AutomaticActionModule GetAutomaticModule(Firearm firearm)
        => firearm is null
            ? null
            : Array.Find(firearm.Base.Modules, module => module is AutomaticActionModule) as AutomaticActionModule;
}
