using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.API.Features.Teams.Definitions.Teams;

public sealed class UIUTeamDefinitionSource : CTeamDefinitionSource
{
    public override IEnumerable<CTeamDefinition> GetDefinitions()
    {
        yield return Define(CTeam.UIU, "連邦捜査局(FBI)異常事件課", "U I U", "", true);
    }
}
