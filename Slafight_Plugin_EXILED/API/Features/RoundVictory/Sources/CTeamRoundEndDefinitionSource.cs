using System.Collections.Generic;
using System.Linq;
using Slafight_Plugin_EXILED.API.Features.RoundVictory.Core;
using Slafight_Plugin_EXILED.API.Features.Teams;
using Slafight_Plugin_EXILED.API.Features.Teams.Core;

namespace Slafight_Plugin_EXILED.API.Features.RoundVictory.Sources;

public sealed class CTeamRoundEndDefinitionSource : RoundEndDefinitionSource
{
    public override IEnumerable<RoundEndDefinition> GetTeamDefaults()
    {
        return CTeamRegistry.All
            .Select(definition => definition.RoundEndDefinition)
            .Where(definition => definition != null);
    }
}
