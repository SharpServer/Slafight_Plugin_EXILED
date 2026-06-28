using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.RoundVictory.Core;

namespace Slafight_Plugin_EXILED.API.Features.RoundVictory.Sources;

public sealed class SpecificRoundEndDefinitionSource : RoundEndDefinitionSource
{
    public override IEnumerable<RoundEndDefinition> GetSpecificDefinitions()
    {
        yield return RoundEndDefinition.Custom(
            "FacilityTerminationNormalcyWin",
            CTeam.FoundationForces,
            "<b><size=80><color=red>正常性</color>の勝利</size></b>",
            specificReason: RoundEndReasons.NoHumanityAllowed,
            beforeEnd: ResetSpawnContext);
        
        yield return RoundEndDefinition.Custom(
            "FacilityTerminationHumanityWin",
            CTeam.GoC,
            "<b><size=80><color=#0000c8>人類</color>の勝利</size></b>",
            specificReason: RoundEndReasons.SavedHumanity,
            beforeEnd: ResetSpawnContext);

        yield return RoundEndDefinition.Custom(
            "SnowWarriorWin",
            CTeam.Others,
            "<b><size=80><color=#ffffff>雪の戦士達</color>の勝利</size></b>",
            specificReason: RoundEndReasons.SnowWarriorWin);

        yield return RoundEndDefinition.Custom(
            "CandyWarriorWin",
            CTeam.Others,
            "<b><size=80><color=#ff96de>お菓子の戦士達</color>の勝利</size></b>",
            specificReason: RoundEndReasons.CandyWarriorWin);

        yield return RoundEndDefinition.Custom(
            "DanteWin",
            CTeam.Others,
            "<b><size=80><color=#ff1a1a>DANTE</color>の勝利</size></b>",
            specificReason: RoundEndReasons.DanteWin);
    }

    private static void ResetSpawnContext()
    {
        SpawnContextRegistry.SetActive("Default");
    }
}
