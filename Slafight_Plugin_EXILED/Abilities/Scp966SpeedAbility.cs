using Exiled.API.Enums;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.Abilities;

public class Scp966SpeedAbility : AbilityBase
{
    // デフォルトのクールダウンと回数
    protected override float DefaultCooldown => 20f; // 20秒クールダウン
    protected override int DefaultMaxUses => -1;     // 無制限

    // 速度パラメータ
    private const byte SpeedBoostIntensity = 25;  // MovementBoost 強度
    private const byte BaseSlownessIntensity = 40;

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

        // 基礎 Slowness を消して MovementBoost を付ける
        player.DisableEffect(EffectType.Slowness);
        player.EnableEffect(EffectType.MovementBoost, SpeedBoostIntensity);

        // HUD など
        player.ShowRueiPlus("<color=yellow>SCP-966 高速移動開始！攻撃すると解除。</color>", 3f);

        // 実際の持続時間は Cooldown とは別に決めたいならここで
        // 例えば 8秒だけ高速にするなら:
        // Timing.CallDelayed(8f, () => StopSpeed(player));
    }

    protected override void OnCooldownEnd(Player player)
    {
        // 通常のヒント処理 + 名前差し替え
        if (player != null && player.IsConnected &&
            AbilityManager.TryGetLoadout(player, out var loadout) &&
            loadout.Slots[loadout.ActiveIndex] == this)
        {
            player.ShowRueiPlus(
                "<color=yellow>高速移動アビリティのクールダウンが終了しました。</color>",
                3f);
        }
    }

    public static void StopSpeed(Player player)
    {
        if (player == null || !player.IsConnected)
            return;

        // MovementBoost を消して再度 Slowness を付ける
        player.DisableEffect(EffectType.MovementBoost);
        player.EnableEffect(EffectType.Slowness, BaseSlownessIntensity);
    }

    // 966 が誰かを攻撃したときに呼んで即解除したい場合用
    public static void OnAttackedCancelSpeed(Player scp966)
    {
        StopSpeed(scp966);
        // 必要ならここでクールダウンリセット/扱い追加
        // ResetCooldown(scp966.Id, typeof(Scp966SpeedAbility));
    }
}
