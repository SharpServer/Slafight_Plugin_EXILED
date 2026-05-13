using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Structs;
using InventorySystem.Items.Armor;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class ArmorInfantry : CItemArmor
{
    public override string DisplayName => "歩兵用アーマー";
    public override string Description => "大規模な部隊の歩兵に使われる戦闘アーマー。";

    protected override string   UniqueKey            => "ArmorInfantry";
    protected override ItemType BaseItem             => ItemType.ArmorCombat;

    protected override int   VestEfficacy        => 80;
    protected override int   HelmetEfficacy       => 85;
    protected override float StaminaUseMultiplier => 0.15f;

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => Color.cyan;

    protected override IReadOnlyList<ArmorAmmoLimit> AmmoLimits =>
    [
        new() { AmmoType = AmmoType.Nato9,   Limit = 220 },
        new() { AmmoType = AmmoType.Nato762, Limit = 130 },
        new() { AmmoType = AmmoType.Ammo12Gauge, Limit = 80  },
    ];

    protected override IReadOnlyList<BodyArmor.ArmorCategoryLimitModifier> CategoryLimits =>
    [
        new() { Category = ItemCategory.Firearm, Limit = 3 },
        new() { Category = ItemCategory.Grenade, Limit = 3 },
    ];
}