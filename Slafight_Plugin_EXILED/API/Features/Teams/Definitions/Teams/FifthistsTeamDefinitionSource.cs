using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.RoundVictory.Core;
using Slafight_Plugin_EXILED.API.Features.Teams.Core;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.API.Features.Teams.Definitions.Teams;

public sealed class FifthistsTeamDefinitionSource : CTeamDefinitionSource
{
    public override IEnumerable<CTeamDefinition> GetDefinitions()
    {
        yield return Define(
            CTeam.Fifthists,
            "第五教会",
            "$pitch_1.05 5 5 5 $pitch_1 Forces",
            "#ff00fa",
            true,
            victoryRule: RoundVictoryRule.ForPredicate(
                "FifthistWin",
                CTeam.Fifthists,
                player => player.GetTeam() == CTeam.Fifthists ||
                          player.GetCustomRole() == CRoleTypeId.Scp3005,
                priority: 10,
                requiresVanillaEndLock: true),
            roundEndDefinition: RoundEndDefinition.Custom(
                "FifthistWin",
                CTeam.Fifthists,
                "<b><size=80><color=#ff00fa>第五教会</color>の勝利</size></b>",
                RoundEndScoreMode.KillsByScp,
                555));
    }
}
