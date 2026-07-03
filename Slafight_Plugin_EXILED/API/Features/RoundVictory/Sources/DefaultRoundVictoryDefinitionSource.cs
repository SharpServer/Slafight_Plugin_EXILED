using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.RoundVictory.Core;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.API.Features.RoundVictory.Sources;

public sealed class DefaultRoundVictoryDefinitionSource : RoundVictoryDefinitionSource
{
    public override bool IsEnabled(RoundVictoryContext context) =>
        context.ActiveEvent is not SpecialEventType.FacilityTermination;

    public override IEnumerable<RoundVictoryCondition> GetConditions()
    {
        yield return new RoundVictoryCondition(
            CTeam.SCPs,
            "AIWin",
            context => RoundVictoryDefinitions.HasOnlyTargetRoleOnSideAgainstSingleOpposingTeam(
                context.AlivePlayers,
                player => RoundVictoryDefinitions.GetVictoryTeam(player) == CTeam.SCPs,
                RoundVictoryDefinitions.IsScp079Player),
            RoundVictoryDefinitions.ExecuteAIKill,
            isForEnd: false);

        yield return new RoundVictoryCondition(
            CTeam.FoundationForces,
            "AraOrunDeath",
            context => RoundVictoryDefinitions.HasOnlyTargetRoleOnSideAgainstSingleOpposingTeam(
                context.AlivePlayers,
                player => !RoundVictoryDefinitions.GetVictoryTeam(player).IsGoI(),
                RoundVictoryDefinitions.IsAraOrunPlayer),
            RoundVictoryDefinitions.ExecuteAraOrunKill,
            context => context.ActiveEvent is SpecialEventType.CaseColourlessGreen,
            isForEnd: false);
    }

}
