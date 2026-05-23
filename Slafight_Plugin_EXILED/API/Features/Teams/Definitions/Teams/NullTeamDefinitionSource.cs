using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.API.Features.Teams.Definitions.Teams;

public sealed class NullTeamDefinitionSource : CTeamDefinitionSource
{
    public override IEnumerable<CTeamDefinition> GetDefinitions()
    {
        yield return Define(CTeam.Null, "不明な勢力", "Unknown Forces", "#ffffff", true);
    }
}
