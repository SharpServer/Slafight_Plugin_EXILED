using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.RoundVictory.Core;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.API.Features.RoundVictory.Sources;

public sealed class SeasonalWarrierVictoryDefinitionSource : RoundVictoryDefinitionSource
{
    public override bool IsEnabled(RoundVictoryContext context) =>
        context.ActiveEvent is not SpecialEventType.FacilityTermination;

    public override IEnumerable<RoundVictoryGroup> GetGroups()
    {
        yield return RoundVictoryRule.ForCustomRoles(
            CTeam.Others,
            priority: 20,
            customRoles: [CRoleTypeId.SnowWarrier],
            debugName: "SnowWarrierWin",
            specificReason: RoundEndReasons.SnowWarrierWin,
            requiresVanillaEndLock: true,
            isEnabled: context => context.Season == SeasonTypeId.Christmas).ToGroup();

        yield return RoundVictoryRule.ForPredicate(
            "CandyWarrierWin",
            CTeam.Others,
            player => player.IsCandyWarrier(),
            priority: 20,
            specificReason: RoundEndReasons.CandyWarrierWin,
            requiresVanillaEndLock: true,
            isEnabled: context => context.Season is SeasonTypeId.April or SeasonTypeId.Halloween).ToGroup();
    }
}
