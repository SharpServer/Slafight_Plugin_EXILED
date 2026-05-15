using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class GunCOM77 : CItemWeapon
{
    public override string DisplayName => "COM-77";
    public override string Description => string.Empty;

    protected override string UniqueKey => "GunCOM77";
    protected override ItemType BaseItem => ItemType.GunCOM18;

    protected override float Damage => 25f;
    protected override byte MagazineSize => 7;
    protected override Vector3 Scale => new(1f, 1f, 1.15f);

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => Color.white;

    /// <summary>
    /// 元実装はリロード開始時にマガジンを MagazineSize に張り替え常に通す挙動。
    /// CItemWeapon 標準のリロード抑止 (TotalAmmo >= MagazineSize で禁止) を
    /// バイパスしたいので OnReloading 段階で Allow に倒す。
    /// </summary>
    protected override void OnReloading(ReloadingWeaponEventArgs ev)
    {
        ev.Firearm.MagazineAmmo = MagazineSize;
        ev.IsAllowed = true;
    }

    protected override bool ShouldBlockReloading(ReloadingWeaponEventArgs ev) => false;
}
