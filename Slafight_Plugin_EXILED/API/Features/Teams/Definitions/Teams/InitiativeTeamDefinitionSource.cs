using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.RoundVictory.Core;
using Slafight_Plugin_EXILED.API.Features.Teams.Core;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.API.Features.Teams.Definitions.Teams;

public sealed class InitiativeTeamDefinitionSource : CTeamDefinitionSource
{
    public override IEnumerable<CTeamDefinition> GetDefinitions()
    {
        yield return Define(
            CTeam.Initiative,
            "境界線イニシアチブ",
            "X Power Forces",
            $"{ServerColors.BlueGreen}",
            true,
            victoryRule: RoundVictoryRule.ForPredicate(
                "InitiativeWin",
                CTeam.Initiative,
                player => player.GetTeam() == CTeam.Initiative,
                priority: 10,
                requiresVanillaEndLock: true),
            roundEndDefinition: RoundEndDefinition.Custom(
                "InitiativeWin",
                CTeam.Initiative,
                $"<b><size=80><color={ServerColors.BlueGreen}>境界線イニシアチブ</color>の勝利</size></b>",
                RoundEndScoreMode.KillsByScp,
                555));
    }
}
