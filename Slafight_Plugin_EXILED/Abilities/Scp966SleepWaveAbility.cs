using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomRoles.SCPs;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.Abilities;

public class Scp966SleepWaveAbility : AbilityBase
{
    protected override float DefaultCooldown => 55f;
    protected override int DefaultMaxUses => -1;

    private const float BurstRange = 20f;
    private const float BurstDebt = 32f;

    public Scp966SleepWaveAbility(Player owner)
        : base(owner) { }

    public Scp966SleepWaveAbility(Player owner, float cooldownSeconds)
        : base(owner, cooldownSeconds) { }

    public Scp966SleepWaveAbility(Player owner, float cooldownSeconds, int maxUses)
        : base(owner, cooldownSeconds, maxUses) { }

    protected override void ExecuteAbility(Player player)
    {
        if (player == null || !player.IsConnected)
            return;

        if (player.GetCustomRole() != CRoleTypeId.Scp966)
            return;

        var count = Scp966Role.EmitSleepWave(player, BurstDebt, BurstRange, true);
        EffectedInfoTextProvider.Set(player,
            count > 0
                ? $"<color=#ff9966>██████波を放射。{count}人の睡眠段階を破壊した。</color>"
                : "<color=#888888>██████波は空振りした。20m以内に獲物がいない。</color>",
            4f);
    }

    protected override void OnCooldownEnd(Player player)
    {
        if (player != null && player.IsConnected &&
            AbilityManager.TryGetLoadout(player, out var loadout) &&
            loadout.Slots[loadout.ActiveIndex] == this)
        {
            EffectedInfoTextProvider.Set(player, "<color=#ff9966>██████波の再放射が可能。</color>", 3f);
        }
    }
}
