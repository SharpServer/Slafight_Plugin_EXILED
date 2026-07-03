using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.RoundVictory.Core;
using Slafight_Plugin_EXILED.API.Features.Teams.Core;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.API.Features.Teams.Definitions.Teams;

public sealed class ScpsTeamDefinitionSource : CTeamDefinitionSource
{
    public override IEnumerable<CTeamDefinition> GetDefinitions()
    {
        yield return Define(
            CTeam.SCPs,
            "SCP",
            "SCP",
            "#c50000",
            true,
            victoryRule: RoundVictoryRule.ForPredicate(
                "ScpWin",
                CTeam.SCPs,
                player => RoundVictoryDefinitions.GetVictoryTeam(player) == CTeam.SCPs &&
                          player.GetCustomRole() is not (CRoleTypeId.Scp3005 or CRoleTypeId.Scp999),
                priority: 40),
            roundEndDefinition: RoundEndDefinition.Vanilla(
                "ScpWin",
                CTeam.SCPs));
    }
}
