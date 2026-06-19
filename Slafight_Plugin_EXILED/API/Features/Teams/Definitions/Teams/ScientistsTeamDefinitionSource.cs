using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.RoundVictory.Core;
using Slafight_Plugin_EXILED.API.Features.Teams.Core;

namespace Slafight_Plugin_EXILED.API.Features.Teams.Definitions.Teams;

public sealed class ScientistsTeamDefinitionSource : CTeamDefinitionSource
{
    public override IEnumerable<CTeamDefinition> GetDefinitions()
    {
        yield return Define(
            CTeam.Scientists,
            "科学者",
            "Scientist Personnel",
            "#faff86",
            false,
            roundEndDefinition: RoundEndDefinition.Vanilla(
                "ScientistsWin",
                CTeam.Scientists));
    }
}
