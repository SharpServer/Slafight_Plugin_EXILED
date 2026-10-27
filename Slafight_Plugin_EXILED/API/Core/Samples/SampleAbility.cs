using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.API.Core.Samples;

/// <summary>
/// 能力の書き方の見本です。入力は <see cref="InputHandler"/> が扱います。
/// </summary>
public sealed class SampleAbility : AbilityBase
{
    public override string Name => "Sample Dash";

    public override string Description => "前方へ短く踏み込みます。";

    public override float Cooldown => 8f;

    public override int MaxUses => 5;

    protected override void OnUsed()
    {
        Player.Position += Player.GameObject.transform.forward * 4f;
    }

    /// <summary>
    /// 基底の判定 (生存・回数・クールダウン) に、この能力だけの条件を足します。
    /// </summary>
    protected override bool CanUse(out string failureReason)
    {
        if (!base.CanUse(out failureReason))
            return false;

        if (Player.IsCuffed)
        {
            failureReason = "拘束されている間は使えません。";

            return false;
        }

        return true;
    }
}
