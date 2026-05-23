using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.RoundVictory;

namespace Slafight_Plugin_EXILED.API.Features.Teams.Definitions.Teams;

public sealed class ClassDTeamDefinitionSource : CTeamDefinitionSource
{
    public override IEnumerable<CTeamDefinition> GetDefinitions()
    {
        yield return Define(
            CTeam.ClassD,
            "Dクラス職員",
            "Class D Personnel",
            "#ee7600",
            true,
            roundEndDefinition: RoundEndDefinition.Vanilla(
                "ClassDWin",
                CTeam.ClassD,
                RoundEndScoreMode.EscapedDClasses));
    }
}
