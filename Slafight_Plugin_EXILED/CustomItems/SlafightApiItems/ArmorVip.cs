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

    // 標準弾道防護: 実軽減率 = Efficacy × (1 - 弾丸の貫通率)
    protected override int   VestBallisticEfficacy   => 100;
    protected override int   HelmetBallisticEfficacy => 100;
    protected override float StaminaUseMultiplier => 0.2f;

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => CustomColor.Purple.ToUnityColor();

    protected override IReadOnlyList<ArmorAmmoLimit> AmmoLimits =>
    [
        AmmoLimit(AmmoType.Nato9, 400),
        AmmoLimit(AmmoType.Nato556, 400),
        AmmoLimit(AmmoType.Ammo12Gauge, 100),
        AmmoLimit(AmmoType.Ammo44Cal, 50),
    ];

    protected override IReadOnlyList<BodyArmor.ArmorCategoryLimitModifier> CategoryLimits =>
    [
        CategoryLimit(ItemCategory.Firearm, 3),
        CategoryLimit(ItemCategory.Grenade, 3),
    ];
}
