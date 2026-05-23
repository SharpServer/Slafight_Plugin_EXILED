using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.Teams.Core;

namespace Slafight_Plugin_EXILED.API.Features.Teams.Definitions.Teams;

public sealed class O5TeamDefinitionSource : CTeamDefinitionSource
{
    public override IEnumerable<CTeamDefinition> GetDefinitions()
    {
        yield return Define(CTeam.O5, "O5評議会", "O5 Command", "#000000", true);
    }
}
