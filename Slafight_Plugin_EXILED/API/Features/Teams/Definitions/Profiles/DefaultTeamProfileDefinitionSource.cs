using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.API.Features.Teams.Definitions.Profiles;

public sealed class DefaultTeamProfileDefinitionSource : CTeamProfileDefinitionSource
{
    public override IEnumerable<CTeamProfileDefinition> GetDefinitions()
    {
        yield return Define(
            CTeamProfileManager.DefaultProfile,
            [
                Victory(
                    CTeamGroup.Foundation,
                    "FoundationWin",
                    CTeam.FoundationForces,
                    priority: 50),
                Victory(
                    CTeamGroup.Insurgency,
                    "ChaosWin",
                    CTeam.ChaosInsurgency,
                    priority: 60)
            ],
            Map(CTeam.FoundationForces, CTeamGroup.Foundation),
            Map(CTeam.Scientists, CTeamGroup.Foundation),
            Map(CTeam.Guards, CTeamGroup.Foundation),
            Map(CTeam.ChaosInsurgency, CTeamGroup.Insurgency),
            Map(CTeam.ClassD, CTeamGroup.Insurgency),
            Map(CTeam.Null, CTeamGroup.Undefined),
            Map(CTeam.Fifthists, CTeamGroup.Undefined),
            Map(CTeam.GoC, CTeamGroup.Undefined),
            Map(CTeam.UIU, CTeamGroup.Undefined),
            Map(CTeam.SerpentsHand, CTeamGroup.Undefined),
            Map(CTeam.SCPs, CTeamGroup.Undefined),
            Map(CTeam.Others, CTeamGroup.Undefined),
            Map(CTeam.BrokenGodChurch, CTeamGroup.Undefined),
            Map(CTeam.O5, CTeamGroup.Undefined),
            Map(CTeam.Sarkic, CTeamGroup.Undefined),
            Map(CTeam.AWCY, CTeamGroup.Undefined),
            Map(CTeam.BlackQueen, CTeamGroup.Undefined));
    }
}
