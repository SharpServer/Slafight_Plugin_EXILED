using System;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Features;

namespace Slafight_Plugin_EXILED.Extensions;

public static class PlayerAbilityExtensions
{
    // アビリティ追加（スロット制限付き）
    public static bool AddAbility<TAbility>(this Player? player, float? cooldownOverride = null, int? maxUsesOverride = null)
        where TAbility : AbilityBase, new()
    {
        if (player == null)
            return false;

        Log.Debug($"[Ability] Add {typeof(TAbility).Name} to {player.Nickname}");
        return player.AddAbility(new TAbility(), cooldownOverride, maxUsesOverride);
    }

    // 直接インスタンス渡し版
    public static bool AddAbility(
        this Player? player,
        AbilityBase? ability,
        float? cooldownOverride = null,
        int? maxUsesOverride = null)
    {
        if (player == null || ability == null)
            return false;

        var loadout = AbilityManager.GetOrCreateLoadout(player);
        if (loadout == null)
            return false;

        if (!loadout.HasFreeSlot())
            return false;

        try
        {
            ability.Initialize(player, cooldownOverride, maxUsesOverride);
        }
        catch (Exception ex)
        {
            Log.Warn($"[Ability] Failed to initialize {ability.GetType().Name} for {player.Nickname}: {ex}");
            return false;
        }

        var added = loadout.AddAbility(ability);
        if (!added)
        {
            AbilityBase.RevokeAbility(player.Id, ability.GetType());
            return false;
        }

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

        AbilityBase.RevokeAbility(player.Id, typeof(TAbility));
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
