using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.RoundVictory;

namespace Slafight_Plugin_EXILED.API.Features.Teams.Definitions.Teams;

public sealed class FoundationForcesTeamDefinitionSource : CTeamDefinitionSource
{
    public override IEnumerable<CTeamDefinition> GetDefinitions()
    {
        yield return Define(
            CTeam.FoundationForces,
            "機動部隊",
            "MtfUnit",
            "#00b7eb",
            false,
            roundEndDefinition: RoundEndDefinition.Vanilla(
                "FoundationWin",
                CTeam.FoundationForces,
                RoundEndScoreMode.EscapedScientists));
    }
}
