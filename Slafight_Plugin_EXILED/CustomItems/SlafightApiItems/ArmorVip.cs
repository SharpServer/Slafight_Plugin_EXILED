using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Structs;
using InventorySystem.Items.Armor;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Enums;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class ArmorVip : CItemArmor
{
    public override string DisplayName => "要人用アーマー";
    public override string Description => "要人の命を守るために、防護に超特化したアーマー。";

    protected override string   UniqueKey            => "ArmorVip";
    protected override ItemType BaseItem             => ItemType.ArmorHeavy;

    protected override int   VestEfficacy        => 100;
    protected override int   HelmetEfficacy       => 100;
    protected override float StaminaUseMultiplier => 0.2f;

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => CustomColor.Purple.ToUnityColor();

    protected override IReadOnlyList<ArmorAmmoLimit> AmmoLimits =>
    [
        new() { AmmoType = AmmoType.Nato9,    Limit = 400 },
        new() { AmmoType = AmmoType.Nato556,  Limit = 400 },
        new() { AmmoType = AmmoType.Ammo12Gauge, Limit = 100 },
        new() { AmmoType = AmmoType.Ammo44Cal,   Limit = 50  },
    ];

    protected override IReadOnlyList<BodyArmor.ArmorCategoryLimitModifier> CategoryLimits =>
    [
        new() { Category = ItemCategory.Firearm, Limit = 3 },
        new() { Category = ItemCategory.Grenade, Limit = 3 },
    ];
}