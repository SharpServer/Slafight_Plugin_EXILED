using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.RoundVictory;
using Slafight_Plugin_EXILED.API.Features.RoundVictory.Core;
using Slafight_Plugin_EXILED.API.Features.Teams.Core;

namespace Slafight_Plugin_EXILED.API.Features.Teams.Definitions.Teams;

public sealed class GuardsTeamDefinitionSource : CTeamDefinitionSource
{
    public override IEnumerable<CTeamDefinition> GetDefinitions()
    {
        yield return Define(
            CTeam.Guards,
            "警備員",
            "Facility Guard Personnel",
            "#00b7eb",
            false,
            roundEndDefinition: RoundEndDefinition.Vanilla(
                "GuardsWin",
                CTeam.Guards,
                RoundEndScoreMode.EscapedScientists));
    }
}
