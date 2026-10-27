using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.API.Core.Samples;

/// <summary>
/// 陣営の書き方の見本です。<see cref="IncludesVanilla"/> はカスタム役職を持たないプレイヤーだけを判定します。
/// </summary>
public sealed class SampleTeam : CustomTeam
{
    public override string Name => "Sample Team";

    public override string Color => ServerColors.Cyan;

    public override string Objective => "動作確認用の陣営です。";

    /// <summary>
    /// 敵対する側が全滅したら勝ち、という最も普通の条件です。
    /// </summary>
    public override VictoryCondition Victory => VictoryCondition.LastStanding(priority: 1);

    /// <summary>
    /// 味方一覧を出すかどうかも陣営が名乗ります。表示層に陣営ごとの分岐は要りません。
    /// </summary>
    public override bool ShowsRoster => true;

    /// <summary>
    /// 一覧の末尾に添える、この陣営だけの状況表示です。
    /// </summary>
    public override string RosterFooter(Player viewer) => "<color=#00b7eb>動作確認中</color>";

    protected override bool IncludesVanilla(Player player) => player.Role.Type is RoleTypeId.Tutorial;
}
