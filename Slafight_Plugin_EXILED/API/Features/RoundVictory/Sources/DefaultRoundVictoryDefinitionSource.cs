using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.SpecialEvents;

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
                player => player.GetTeam() == CTeam.SCPs,
                RoundVictoryDefinitions.IsScp079Player),
            RoundVictoryDefinitions.ExecuteAIKill,
            isForEnd: false);

        yield return new RoundVictoryCondition(
            CTeam.FoundationForces,
            "AraOrunDeath",
            context => RoundVictoryDefinitions.HasOnlyTargetRoleOnSideAgainstSingleOpposingTeam(
                context.AlivePlayers,
                player => !player.GetTeam().IsGoI(),
                RoundVictoryDefinitions.IsAraOrunPlayer),
            RoundVictoryDefinitions.ExecuteAraOrunKill,
            context => context.ActiveEvent is SpecialEventType.CaseColourlessGreen,
            isForEnd: false);
    }

}
