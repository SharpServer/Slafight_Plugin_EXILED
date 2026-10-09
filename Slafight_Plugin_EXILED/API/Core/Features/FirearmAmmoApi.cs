using System;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using Slafight_Plugin_EXILED.API.Core.Extensions;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>カスタム銃に適用する弾薬設定です。</summary>
public sealed class FirearmAmmoSettings
{
    public FirearmAmmoSettings(
        int? magazineCapacity = null,
        int? initialMagazineAmmo = null,
        ItemType? ammoItemType = null,
        int ammoDrain = 1)
    {
        MagazineCapacity = magazineCapacity;
        InitialMagazineAmmo = initialMagazineAmmo ?? magazineCapacity;
        if (ammoItemType == ItemType.None)
            throw new ArgumentOutOfRangeException(nameof(ammoItemType), ammoItemType, "Ammo item type cannot be None.");

        AmmoItemType = ammoItemType;
        AmmoDrain = Math.Max(1, ammoDrain);
    }

    /// <summary>アタッチメント補正後の実効マガジン容量です。</summary>
    public int? MagazineCapacity { get; }
    public int? InitialMagazineAmmo { get; }
    /// <summary>この銃が消費する予備弾薬の ItemType です。</summary>
    public ItemType? AmmoItemType { get; }
    public int AmmoDrain { get; }
}

/// <summary>モード切替時に引き継ぐ銃の弾薬状態です。</summary>
public sealed class FirearmAmmoState
{
    internal FirearmAmmoState(int magazineAmmo, int chamberedAmmo, int barrelAmmo, bool magazineInserted)
    {
        MagazineAmmo = magazineAmmo;
        ChamberedAmmo = chamberedAmmo;
        BarrelAmmo = barrelAmmo;
        MagazineInserted = magazineInserted;
    }

    public int MagazineAmmo { get; }
    public int ChamberedAmmo { get; }
    public int BarrelAmmo { get; }
    public bool MagazineInserted { get; }
}

/// <summary>
/// カスタム銃の弾種・容量・装填状態を、インベントリ、Pickup、Hybrid 復元で
/// 同じ規則にそろえて扱います。
/// </summary>
public static class FirearmAmmoApi
{
    /// <summary>互換用の静的入口です。新規コードでは <c>firearm.ApplyAmmoSettings</c> を使ってください。</summary>
    public static void Apply(Firearm firearm, FirearmAmmoSettings settings)
        => firearm.ApplyAmmoSettings(settings);

    /// <summary>互換用の静的入口です。新規コードでは <c>pickup.ApplyAmmoSettings</c> を使ってください。</summary>
    public static void Apply(FirearmPickup pickup, FirearmAmmoSettings settings)
        => pickup.ApplyAmmoSettings(settings);

    public static void Initialize(Firearm firearm, FirearmAmmoSettings settings)
        => firearm.InitializeAmmo(settings);

    public static FirearmAmmoState Capture(Firearm firearm)
        => firearm.CaptureAmmoState();

    public static void Restore(Firearm firearm, FirearmAmmoState state)
        => firearm.RestoreAmmoState(state);

    public static void PrepareForUse(Firearm firearm)
        => firearm.PrepareForUse();

    public static void SetAmmoItemType(Firearm firearm, ItemType ammoItemType)
        => firearm.SetAmmoItemType(ammoItemType);
}
