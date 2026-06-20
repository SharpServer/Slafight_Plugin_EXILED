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

    // 標準弾道防護: 実軽減率 = Efficacy × (1 - 弾丸の貫通率)
    protected override int   VestBallisticEfficacy   => 80;
    protected override int   HelmetBallisticEfficacy => 85;
    protected override float StaminaUseMultiplier => 0.15f;

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => Color.cyan;

    protected override IReadOnlyList<ArmorAmmoLimit> AmmoLimits =>
    [
        AmmoLimit(AmmoType.Nato9, 220),
        AmmoLimit(AmmoType.Nato556, 200),
        AmmoLimit(AmmoType.Nato762, 130),
        AmmoLimit(AmmoType.Ammo12Gauge, 80),
    ];

    protected override IReadOnlyList<BodyArmor.ArmorCategoryLimitModifier> CategoryLimits =>
    [
        CategoryLimit(ItemCategory.Firearm, 3),
        CategoryLimit(ItemCategory.Grenade, 3),
    ];
}
