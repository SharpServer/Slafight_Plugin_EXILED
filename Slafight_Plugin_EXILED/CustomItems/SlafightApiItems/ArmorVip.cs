using Exiled.API.Enums;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class ArmorVip : CItemArmor
{
    public override string DisplayName => "要人用アーマー";
    public override string Description => "要人の命を守るために、防護に超特化したアーマー。";

    protected override string UniqueKey => "ArmorVip";
    protected override ItemType BaseItem => ItemType.ArmorHeavy;

    protected override int    VestEfficacy        => 100;
    protected override int    HelmetEfficacy      => 100;
    protected override float  StaminaUseMultiplier => 0.2f;

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => CustomColor.Purple.ToUnityColor();

    /// <summary>装備時に Nato9/Nato556 上限と Firearm/Grenade 枠を増やす。</summary>
    protected override void OnAcquired(ItemAddedEventArgs ev, bool displayMessage)
    {
        ev.Player.SetAmmoLimit(AmmoType.Nato9, 400);
        ev.Player.SetAmmoLimit(AmmoType.Nato556, 400);
        ev.Player.SetAmmoLimit(AmmoType.Ammo12Gauge, 100);
        ev.Player.SetAmmoLimit(AmmoType.Ammo44Cal, 50);
        ev.Player.SetCategoryLimit(ItemCategory.Firearm, 3);
        ev.Player.SetCategoryLimit(ItemCategory.Grenade, 3);
    }

    /// <summary>取り外し時に拡張を解除。</summary>
    protected override void OnDropping(DroppingItemEventArgs ev)
    {
        ev.Player.ResetAmmoLimit(AmmoType.Nato9);
        ev.Player.ResetAmmoLimit(AmmoType.Nato556);
        ev.Player.ResetAmmoLimit(AmmoType.Ammo12Gauge);
        ev.Player.ResetAmmoLimit(AmmoType.Ammo44Cal);
        ev.Player.ResetCategoryLimit(ItemCategory.Firearm);
        ev.Player.ResetCategoryLimit(ItemCategory.Grenade);
    }
}
