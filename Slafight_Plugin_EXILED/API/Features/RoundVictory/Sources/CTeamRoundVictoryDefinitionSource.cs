using System.Collections.Generic;
using System.Linq;
using Slafight_Plugin_EXILED.API.Features.Teams;

namespace Slafight_Plugin_EXILED.API.Features.RoundVictory.Sources;

public sealed class CTeamRoundVictoryDefinitionSource : RoundVictoryDefinitionSource
{
    public override IEnumerable<RoundVictoryGroup> GetGroups()
    {
        return CTeamProfileRegistry.All
            .SelectMany(profile => profile.Victories.Values)
            .Select(definition => definition.ToVictoryRule().ToGroup())
            .Concat(CTeamRegistry.All
                .Select(definition => definition.VictoryRule)
                .Where(rule => rule != null)
                .Select(rule => rule.ToGroup()));
    }
}
