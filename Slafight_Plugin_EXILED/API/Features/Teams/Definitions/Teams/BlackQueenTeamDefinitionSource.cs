using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.API.Features.Teams.Definitions.Teams;

public sealed class BlackQueenTeamDefinitionSource : CTeamDefinitionSource
{
    public override IEnumerable<CTeamDefinition> GetDefinitions()
    {
        yield return Define(CTeam.BlackQueen, "黒の女王", "Black Q been", "#000000", true);
    }
}
