using System.Collections.Generic;
using Exiled.API.Features.Items;
using Exiled.API.Structs;
using InventorySystem.Items.Armor;

using ExiledItem = Exiled.API.Features.Items.Item;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// <see cref="CustomItem"/> のボディアーマー向け基底です。
/// 装備制限・弾薬容量・スタミナ特性を付与と再拾得のたびに復元します。
/// </summary>
public abstract class CustomArmor : CustomItem
{
    protected virtual int? HelmetEfficacy => null;
    protected virtual int? VestEfficacy => null;
    protected virtual float? Weight => null;
    protected virtual float? StaminaUseMultiplier => null;
    protected virtual float? StaminaRegenMultiplier => null;
    protected virtual IEnumerable<ArmorAmmoLimit> AmmoLimits => null;
    protected virtual IEnumerable<BodyArmor.ArmorCategoryLimitModifier> CategoryLimits => null;

    protected override void Customize(LabApi.Features.Wrappers.Item item)
    {
        if (ExiledItem.Get(item.Base) is Armor armor)
            Apply(armor);

        base.Customize(item);
    }

    /// <summary>アーマー実体へ宣言値を適用します。</summary>
    protected virtual void Apply(Armor armor)
    {
        if (HelmetEfficacy is { } helmet)
            armor.HelmetEfficacy = helmet;
        if (VestEfficacy is { } vest)
            armor.VestEfficacy = vest;
        if (Weight is { } weight)
            armor.Weight = weight;
        if (StaminaUseMultiplier is { } staminaUse)
            armor.StaminaUseMultiplier = staminaUse;
        if (StaminaRegenMultiplier is { } staminaRegen)
            armor.StaminaRegenMultiplier = staminaRegen;
        if (AmmoLimits is { } ammoLimits)
            armor.AmmoLimits = ammoLimits;
        if (CategoryLimits is { } categoryLimits)
            armor.CategoryLimits = categoryLimits;
    }
}
