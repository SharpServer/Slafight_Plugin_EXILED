using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.MainHandlers;

namespace Slafight_Plugin_EXILED.API.Features.RoundVictory.Sources;

public sealed class SpecificRoundEndDefinitionSource : RoundEndDefinitionSource
{
    public override IEnumerable<RoundEndDefinition> GetSpecificDefinitions()
    {
        yield return RoundEndDefinition.Custom(
            "FacilityTerminationNormalcyWin",
            CTeam.FoundationForces,
            "<b><size=80><color=red>正常性</color>の勝利</size></b>",
            RoundEndScoreMode.EscapedScientists,
            specificReason: RoundEndReasons.NoHumanityAllowed,
            beforeEnd: ResetSpawnContext);
        
        yield return RoundEndDefinition.Custom(
            "FacilityTerminationHumanityWin",
            CTeam.GoC,
            "<b><size=80><color=#0000c8>人類</color>の勝利</size></b>",
            RoundEndScoreMode.EscapedDClasses,
            specificReason: RoundEndReasons.SavedHumanity,
            beforeEnd: ResetSpawnContext);

        yield return RoundEndDefinition.Custom(
            "SnowWarrierWin",
            CTeam.Others,
            "<b><size=80><color=#ffffff>雪の戦士達</color>の勝利</size></b>",
            RoundEndScoreMode.EscapedDClasses,
            specificReason: RoundEndReasons.SnowWarrierWin);

        yield return RoundEndDefinition.Custom(
            "CandyWarrierWin",
            CTeam.Others,
            "<b><size=80><color=#ff96de>お菓子の戦士達</color>の勝利</size></b>",
            RoundEndScoreMode.EscapedDClasses,
            specificReason: RoundEndReasons.CandyWarrierWin);
    }

    private static void ResetSpawnContext()
    {
        SpawnContextRegistry.SetActive("Default");
    }
}
