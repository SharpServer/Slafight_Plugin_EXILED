using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.Teams.Core;

namespace Slafight_Plugin_EXILED.API.Features.Teams.Definitions.Teams;

public sealed class SerpentsHandTeamDefinitionSource : CTeamDefinitionSource
{
    public override IEnumerable<CTeamDefinition> GetDefinitions()
    {
        yield return Define(CTeam.SerpentsHand, "サーペント・ハンド", "Serpents Hand", "", true);
    }
}
