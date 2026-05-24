using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.RoundVictory.Core;
using Slafight_Plugin_EXILED.API.Features.Teams.Core;

namespace Slafight_Plugin_EXILED.API.Features.Teams.Definitions.Teams;

public sealed class GoCTeamDefinitionSource : CTeamDefinitionSource
{
    public override IEnumerable<CTeamDefinition> GetDefinitions()
    {
        yield return Define(
            CTeam.GoC,
            "世界オカルト連合",
            "G O C",
            "#0000c8",
            true,
            victoryRule: RoundVictoryRule.ForTeam(
                CTeam.GoC,
                priority: 30,
                debugName: "GoCWin",
                requiresVanillaEndLock: true),
            roundEndDefinition: RoundEndDefinition.Custom(
                "GoCWin",
                CTeam.GoC,
                "<b><size=80><color=#0000c8>世界オカルト連合</color>の勝利</size></b>",
                RoundEndScoreMode.EscapedDClasses));
    }
}
