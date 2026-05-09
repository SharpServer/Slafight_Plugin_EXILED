using Exiled.API.Enums;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class GunSuppressiver : CItemWeapon
{
    public override string DisplayName => "Suppressiver";
    public override string Description => string.Empty;

    protected override string UniqueKey => "GunSuppressiver";
    protected override ItemType BaseItem => ItemType.GunFSP9;

    protected override float Damage => 30f;
    protected override byte MagazineSize => 42;
    protected override Vector3 Scale => new(1f, 1f, 1.15f);

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor => CustomColor.ChaoticGreen.ToUnityColor();

    /// <summary>
    /// 元実装は床から拾った瞬間にプレイヤーへ Nato9=200 / Firearm/Grenade=3 の
    /// インベントリ枠拡張を被せていた。CItem では OnAcquired が同じタイミング。
    /// </summary>
    protected override void OnAcquired(ItemAddedEventArgs ev, bool displayMessage)
    {
        ev.Player.SetAmmoLimit(AmmoType.Nato9, 200);
        ev.Player.SetCategoryLimit(ItemCategory.Firearm, 3);
        ev.Player.SetCategoryLimit(ItemCategory.Grenade, 3);
    }

    /// <summary>手放した瞬間に枠拡張を解除。</summary>
    protected override void OnDropping(DroppingItemEventArgs ev)
    {
        ev.Player.ResetAmmoLimit(AmmoType.Nato9);
        ev.Player.ResetCategoryLimit(ItemCategory.Firearm);
        ev.Player.ResetCategoryLimit(ItemCategory.Grenade);
    }
}
