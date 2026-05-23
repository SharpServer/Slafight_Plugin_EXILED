using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.RoundVictory;

namespace Slafight_Plugin_EXILED.API.Features.Teams.Definitions.Profiles;

public sealed class FacilityTerminationTeamProfileDefinitionSource : CTeamProfileDefinitionSource
{
    public override IEnumerable<CTeamProfileDefinition> GetDefinitions()
    {
        yield return Define(
            CTeamProfileManager.FacilityTerminationProfile,
            new[]
            {
                Victory(
                    CTeamGroup.Humanity,
                    "FacilityTerminationHumanityWin",
                    CTeam.GoC,
                    priority: 10,
                    specificReason: RoundEndReasons.SavedHumanity),
                Victory(
                    CTeamGroup.Normalcy,
                    "FacilityTerminationNormalcyWin",
                    CTeam.FoundationForces,
                    priority: 20,
                    specificReason: RoundEndReasons.NoHumanityAllowed),
            },
            Map(CTeam.FoundationForces, CTeamGroup.Normalcy),
            Map(CTeam.Guards, CTeamGroup.Normalcy),
            Map(CTeam.O5, CTeamGroup.Normalcy),
            Map(CTeam.SCPs, CTeamGroup.Normalcy),
            Map(CTeam.Scientists, CTeamGroup.Humanity),
            Map(CTeam.ClassD, CTeamGroup.Humanity),
            Map(CTeam.ChaosInsurgency, CTeamGroup.Humanity),
            Map(CTeam.GoC, CTeamGroup.Humanity),
            Map(CTeam.UIU, CTeamGroup.Humanity),
            Map(CTeam.SerpentsHand, CTeamGroup.Humanity),
            Map(CTeam.Others, CTeamGroup.Humanity),
            Map(CTeam.BrokenGodChurch, CTeamGroup.Humanity),
            Map(CTeam.Sarkic, CTeamGroup.Humanity),
            Map(CTeam.AWCY, CTeamGroup.Humanity),
            Map(CTeam.BlackQueen, CTeamGroup.Humanity),
            Map(CTeam.Fifthists, CTeamGroup.Undefined),
            Map(CTeam.Null, CTeamGroup.Undefined));
    }
}
