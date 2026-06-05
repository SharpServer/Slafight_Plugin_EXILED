using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using MEC;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Abilities;

public class Scp966SpeedAbility : AbilityBase
{
    protected override float DefaultCooldown => 35f;
    protected override int DefaultMaxUses => -1;

    private const byte SpeedBoostIntensity = 24;
    private const float SprintDuration = 8f;

    private static readonly Dictionary<int, float> SprintEndTimes = new();

    public Scp966SpeedAbility(Player owner)
        : base(owner) { }

    public Scp966SpeedAbility(Player owner, float cooldownSeconds)
        : base(owner, cooldownSeconds) { }

    public Scp966SpeedAbility(Player owner, float cooldownSeconds, int maxUses)
        : base(owner, cooldownSeconds, maxUses) { }

    protected override void ExecuteAbility(Player player)
    {
        if (player == null || !player.IsConnected)
            return;

        if (player.GetCustomRole() != CRoleTypeId.Scp966)
            return;

        SprintEndTimes[player.Id] = Time.time + SprintDuration;
        player.DisableEffect(EffectType.Slowness);
        player.EnableEffect(EffectType.MovementBoost, SpeedBoostIntensity);
        player.EnableEffect(EffectType.SilentWalk, 1, SprintDuration);

        EffectedInfoTextProvider.Set(player,
            "<color=yellow>這いよる混沌: 追跡加速。</color>\n" +
            "<size=22>攻撃すると解除。空洞骨格のため長距離戦には向かない。</size>",
            3f);

        Timing.CallDelayed(SprintDuration, () =>
        {
            if (!IsSprinting(player))
                StopSpeed(player);
        });
    }

    protected override void OnCooldownEnd(Player player)
    {
        // 通常のヒント処理 + 名前差し替え
        if (player != null && player.IsConnected &&
            AbilityManager.TryGetLoadout(player, out var loadout) &&
            loadout.Slots[loadout.ActiveIndex] == this)
        {
            EffectedInfoTextProvider.Set(player, "<color=yellow>高速移動アビリティのクールダウンが終了しました。</color>",
                3f);
        }
    }

    public static void StopSpeed(Player player)
    {
        if (player == null || !player.IsConnected)
            return;

        SprintEndTimes.Remove(player.Id);
        player.DisableEffect(EffectType.MovementBoost);
        player.DisableEffect(EffectType.SilentWalk);
    }

    public static bool IsSprinting(Player player)
    {
        if (player == null)
            return false;

        if (!SprintEndTimes.TryGetValue(player.Id, out var endTime))
            return false;

        if (Time.time <= endTime)
            return true;

        SprintEndTimes.Remove(player.Id);
        return false;
    }

    // 966 が誰かを攻撃したときに呼んで即解除したい場合用
    public static void OnAttackedCancelSpeed(Player scp966)
    {
        StopSpeed(scp966);
    }
}
