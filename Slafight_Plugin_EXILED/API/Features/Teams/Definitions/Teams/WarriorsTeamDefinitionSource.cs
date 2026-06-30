using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.RoundVictory.Core;
using Slafight_Plugin_EXILED.API.Features.Teams.Core;

namespace Slafight_Plugin_EXILED.API.Features.Teams.Definitions.Teams;

public sealed class WarriorsTeamDefinitionSource : CTeamDefinitionSource
{
    public override IEnumerable<CTeamDefinition> GetDefinitions()
    {
        // 季節ごとの個別勝利（雪／お菓子）は SeasonalWarriorVictoryDefinitionSource と
        // SpecificRoundEndDefinitionSource 側で定義する。ここはチームの基本情報と
        // 汎用フォールバックの終了画面のみを持つ。
        yield return Define(
            CTeam.Warriors,
            "戦士達",
            "Warriors",
            "#ffffff",
            true,
            roundEndDefinition: RoundEndDefinition.Custom(
                "WarriorsWin",
                CTeam.Warriors,
                "<b><size=80><color=#ffffff>戦士達</color>の勝利</size></b>"));
    }
}
