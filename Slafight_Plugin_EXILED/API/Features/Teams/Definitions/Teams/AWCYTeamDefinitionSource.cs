using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.Teams.Core;

namespace Slafight_Plugin_EXILED.API.Features.Teams.Definitions.Teams;

public sealed class AWCYTeamDefinitionSource : CTeamDefinitionSource
{
    public override IEnumerable<CTeamDefinition> GetDefinitions()
    {
        yield return Define(CTeam.AWCY, "Are We Cool Yet?", "Are were code yet", "", true);
    }
}
