using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.RoundVictory;

namespace Slafight_Plugin_EXILED.API.Features.Teams.Definitions.Teams;

public sealed class OthersTeamDefinitionSource : CTeamDefinitionSource
{
    public override IEnumerable<CTeamDefinition> GetDefinitions()
    {
        yield return Define(
            CTeam.Others,
            "不明な勢力",
            "Unknown Forces",
            "#ffffff",
            true,
            roundEndDefinition: RoundEndDefinition.Custom(
                "UnknownOthersWin",
                CTeam.Others,
                "<b><size=80><color=#ffffff>UNKNOWN TEAM</color>の勝利</size></b>",
                RoundEndScoreMode.EscapedDClasses));
    }
}
