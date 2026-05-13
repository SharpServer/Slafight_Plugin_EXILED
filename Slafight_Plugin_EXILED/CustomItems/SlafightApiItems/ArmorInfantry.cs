using Exiled.API.Enums;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class ArmorInfantry : CItemArmor
{
    public override string DisplayName => "歩兵用アーマー";
    public override string Description => "大規模な部隊の歩兵に使われる戦闘アーマー。";

    protected override string UniqueKey => "ArmorInfantry";
    protected override ItemType BaseItem => ItemType.ArmorCombat;

    protected override int    VestEfficacy        => 80;
    protected override int    HelmetEfficacy      => 85;
    protected override float  StaminaUseMultiplier => 0.15f;

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => Color.cyan;

    /// <summary>装備時に Nato9 弾持ち上限と Firearm/Grenade 枠を増やす。</summary>
    protected override void OnAcquired(ItemAddedEventArgs ev, bool displayMessage)
    {
        ev.Player.SetAmmoLimit(AmmoType.Nato9, 220);
        ev.Player.SetAmmoLimit(AmmoType.Ammo12Gauge, 80);
        ev.Player.SetCategoryLimit(ItemCategory.Firearm, 3);
        ev.Player.SetCategoryLimit(ItemCategory.Grenade, 3);
    }

    /// <summary>取り外し時に拡張を解除。</summary>
    protected override void OnDropping(DroppingItemEventArgs ev)
    {
        ev.Player.ResetAmmoLimit(AmmoType.Nato9);
        ev.Player.ResetAmmoLimit(AmmoType.Ammo12Gauge);
        ev.Player.ResetCategoryLimit(ItemCategory.Firearm);
        ev.Player.ResetCategoryLimit(ItemCategory.Grenade);
    }
}
