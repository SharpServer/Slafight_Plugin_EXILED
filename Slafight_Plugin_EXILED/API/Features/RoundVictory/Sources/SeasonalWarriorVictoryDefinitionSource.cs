using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.RoundVictory.Core;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.API.Features.RoundVictory.Sources;

public sealed class SeasonalWarriorVictoryDefinitionSource : RoundVictoryDefinitionSource
{
    public override bool IsEnabled(RoundVictoryContext context) =>
        context.ActiveEvent is not SpecialEventType.FacilityTermination;

    public override IEnumerable<RoundVictoryGroup> GetGroups()
    {
        yield return RoundVictoryRule.ForCustomRoles(
            CTeam.Warriors,
            priority: 20,
            customRoles: [CRoleTypeId.SnowWarrior],
            debugName: "SnowWarriorWin",
            specificReason: RoundEndReasons.SnowWarriorWin,
            requiresVanillaEndLock: true,
            isEnabled: context => context.Season == SeasonTypeId.Christmas).ToGroup();

        yield return RoundVictoryRule.ForPredicate(
            "CandyWarriorWin",
            CTeam.Warriors,
            player => player.IsCandyWarrior(),
            priority: 20,
            specificReason: RoundEndReasons.CandyWarriorWin,
            requiresVanillaEndLock: true,
            isEnabled: context => context.Season is SeasonTypeId.April or SeasonTypeId.Halloween).ToGroup();
    }
}
