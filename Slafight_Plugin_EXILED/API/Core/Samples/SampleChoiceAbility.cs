using System.Collections.Generic;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.API.Core.Samples;

/// <summary>
/// 選択肢を持つ能力の書き方の見本です。処理は選択肢自身が持ちます。
/// </summary>
public sealed class SampleChoiceAbility : AbilityBase
{
    public override string Name => "Sample Choice";

    public override string Description => "選んだものを足元に出します。";

    public override float Cooldown => 12f;

    public override IReadOnlyList<AbilityOption> Options =>
    [
        new HealChoice(),
        new LightChoice(),
    ];

    /// <summary>選ばれているものに任せるだけ。</summary>
    protected override void OnUsed() => SelectedOption?.Use(Player);

    private sealed class HealChoice : AbilityOption
    {
        public override string Name => "回復";

        public override string Description => "体力を 25 回復します。";

        public override bool CanUse(Player player, out string failureReason)
        {
            if (player.Health >= player.MaxHealth)
            {
                failureReason = "体力は満タンです。";

                return false;
            }

            failureReason = null;

            return true;
        }

        public override void Use(Player player) => player.Heal(25f);
    }

    private sealed class LightChoice : AbilityOption
    {
        public override string Name => "投光";

        public override string Description => "ライトを 1 つ渡します。";

        public override void Use(Player player) => player.AddItem(ItemType.Flashlight);
    }
}
