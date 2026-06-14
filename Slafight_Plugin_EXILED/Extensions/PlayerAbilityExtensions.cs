using System;
using Slafight_Plugin_EXILED.API.Features;
using Exiled.API.Features;

namespace Slafight_Plugin_EXILED.Extensions;

public static class PlayerAbilityExtensions
{
    // アビリティ追加（スロット制限付き）
    public static bool AddAbility<TAbility>(this Player player)
        where TAbility : AbilityBase
    {
        if (player == null)
            return false;

        Log.Debug($"[Ability] Add {typeof(TAbility).Name} to {player.Nickname}");
        var loadout = AbilityManager.GetOrCreateLoadout(player);
        if (loadout == null)
            return false;

        // TAbility は (Player owner) コンストラクタを持っている前提
        var ability = (TAbility)Activator.CreateInstance(typeof(TAbility), player)!;
        var added = loadout.AddAbility(ability);
        if (added)
            AbilityManager.UpdateAbilityHint(player, loadout);

        return added;
    }

    // 直接インスタンス渡し版
    public static bool AddAbility(this Player player, AbilityBase ability)
    {
        if (player == null || ability == null)
            return false;

        var loadout = AbilityManager.GetOrCreateLoadout(player);
        if (loadout == null)
            return false;

        var added = loadout.AddAbility(ability);
        if (added)
            AbilityManager.UpdateAbilityHint(player, loadout);

        return added;
    }

    // アビリティ削除（型指定）
    public static void RemoveAbility<TAbility>(this Player player)
        where TAbility : AbilityBase
    {
        if (!AbilityManager.TryGetLoadout(player, out var loadout))
            return;

        var removed = false;
        for (int i = 0; i < AbilityLoadout.MaxSlots; i++)
        {
            if (loadout.Slots[i] is TAbility)
            {
                loadout.Slots[i] = null;
                removed = true;
            }
        }

        if (!removed)
            return;

        loadout.EnsureActiveSlot();
        AbilityManager.UpdateAbilityHint(player, loadout);
    }

    // 全アビリティ削除
    public static void ClearAbilities(this Player player)
    {
        AbilityManager.ClearPlayer(player);
    }

    // 現在のアクティブアビリティ発動
    public static void UseActiveAbility(this Player player)
    {
        if (!AbilityManager.TryGetLoadout(player, out var loadout))
            return;

        loadout.ActiveAbility?.TryActivateFromInput(player);
    }

    // アクティブアビリティ切り替え
    public static void NextAbility(this Player player)
    {
        if (!AbilityManager.TryGetLoadout(player, out var loadout))
            return;

        AbilityManager.NextSlot(player);
    }

    public static void ShowAbilityHint(this Player player)
    {
        if (!AbilityManager.TryGetLoadout(player, out var loadout))
            return;
    }
}
