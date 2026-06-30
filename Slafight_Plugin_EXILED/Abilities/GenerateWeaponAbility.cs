using System.Collections.Generic;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.Abilities;

public class GenerateWeaponAbility : OptionAbilityBase
{
    // AbilityBase 抽象プロパティの実装（デフォルト値）
    protected override float DefaultCooldown => 120f;
    protected override int DefaultMaxUses => -1;

    protected override IReadOnlyList<AbilityOption> DefineOptions() =>
    [
        Option("gen_battleaxe", "バトルアックス"),
        Option("gen_throw_knife", "投げナイフ"),
    ];

    protected override bool CanUseOption(Player player, AbilityOption option, out string failureReason)
    {
        if (!base.CanUseOption(player, option, out failureReason))
            return false;

        if (player.IsInventoryFull)
        {
            failureReason = "インベントリが満杯です";
            return false;
        }

        if (option.Is("gen_battleaxe") && player.HasCItem<BattleAxe>())
        {
            failureReason = "既にこのアイテムを所持しています";
            return false;
        }
        if (option.Is("gen_throw_knife") && player.HasCItem<ThrowKnife>())
        {
            failureReason = "既にこのアイテムを所持しています";
            return false;
        }
        
        return true;
    }

    protected override void UseOption(Player player, AbilityOption option)
    {
        if (option.Is("gen_battleaxe"))
        {
            player.GiveCItem<BattleAxe>();
        }
        else if (option.Is("gen_throw_knife"))
        {
            player.GiveCItem<ThrowKnife>();
        }
    }
}
