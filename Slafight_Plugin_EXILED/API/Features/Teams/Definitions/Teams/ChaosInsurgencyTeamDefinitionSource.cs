using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.RoundVictory.Core;
using Slafight_Plugin_EXILED.API.Features.Teams.Core;

namespace Slafight_Plugin_EXILED.API.Features.Teams.Definitions.Teams;

public sealed class ChaosInsurgencyTeamDefinitionSource : CTeamDefinitionSource
{
    public override IEnumerable<CTeamDefinition> GetDefinitions()
    {
        yield return Define(
            CTeam.ChaosInsurgency,
            "カオス・インサージェンシー",
            "Chaos Insurgency",
            "#228b22",
            true,
            roundEndDefinition: RoundEndDefinition.Vanilla(
                "ChaosWin",
                CTeam.ChaosInsurgency));
    }
}
