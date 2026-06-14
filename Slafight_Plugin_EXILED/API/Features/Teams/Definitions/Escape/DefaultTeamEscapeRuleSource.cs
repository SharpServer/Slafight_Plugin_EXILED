using System.Collections.Generic;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.Teams.Escape;
using Slafight_Plugin_EXILED.API.Features.Teams.Profiles;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.API.Features.Teams.Definitions.Escape;

public sealed class DefaultTeamEscapeRuleSource : CTeamEscapeRuleSource
{
    public override IEnumerable<CTeamEscapeRule> GetRules()
    {
        yield return Define(
            "Scp3005Escape",
            0,
            context => context.Player.GetCustomRole() == CRoleTypeId.Scp3005,
            CTeamEscapeTarget.CustomRole(CRoleTypeId.FifthistPriest));

        yield return DefineProfileRule(
            "FacilityTerminationScientistEscape",
            10,
            CTeamProfileManager.FacilityTerminationProfile,
            context => context.PlayerTeam == CTeam.Scientists,
            CTeamEscapeTarget.CustomRole(CRoleTypeId.GoCOperative));

        yield return DefineTeamRule("ClassDEscortFoundation", 100, CTeam.ClassD, FoundationTeams, CTeamEscapeTarget.VanillaRole(RoleTypeId.NtfPrivate));
        yield return DefineTeamRule("ClassDEscortFifthists", 100, CTeam.ClassD, [CTeam.Fifthists], CTeamEscapeTarget.CustomRole(CRoleTypeId.FifthistConvert));
        yield return DefineTeamRule("ClassDEscortGoC", 100, CTeam.ClassD, [CTeam.GoC], CTeamEscapeTarget.CustomRole(CRoleTypeId.GoCOperative));
        yield return DefineTeamRule("ClassDEscapeDefault", 200, CTeam.ClassD, null, CTeamEscapeTarget.VanillaRole(RoleTypeId.ChaosConscript));

        yield return DefineTeamRule("ScientistEscortInsurgency", 100, CTeam.Scientists, InsurgencyTeams, CTeamEscapeTarget.VanillaRole(RoleTypeId.ChaosConscript));
        yield return DefineTeamRule("ScientistEscortFifthists", 100, CTeam.Scientists, [CTeam.Fifthists], CTeamEscapeTarget.CustomRole(CRoleTypeId.FifthistConvert));
        yield return DefineTeamRule("ScientistEscortGoC", 100, CTeam.Scientists, [CTeam.GoC], CTeamEscapeTarget.CustomRole(CRoleTypeId.GoCOperative));
        yield return DefineTeamRule("ScientistEscapeDefault", 200, CTeam.Scientists, null, CTeamEscapeTarget.VanillaRole(RoleTypeId.NtfSpecialist));

        yield return DefineTeamRule("ChaosEscortFoundation", 100, CTeam.ChaosInsurgency, FoundationTeams, CTeamEscapeTarget.VanillaRole(RoleTypeId.NtfPrivate));
        yield return DefineTeamRule("ChaosEscortFifthists", 100, CTeam.ChaosInsurgency, [CTeam.Fifthists], CTeamEscapeTarget.CustomRole(CRoleTypeId.FifthistConvert));
        yield return DefineTeamRule("ChaosEscortGoC", 100, CTeam.ChaosInsurgency, [CTeam.GoC], CTeamEscapeTarget.CustomRole(CRoleTypeId.GoCOperative));

        yield return DefineTeamRule("FoundationEscortInsurgency", 100, CTeam.FoundationForces, InsurgencyTeams, CTeamEscapeTarget.VanillaRole(RoleTypeId.ChaosConscript));
        yield return DefineTeamRule("FoundationEscortFifthists", 100, CTeam.FoundationForces, [CTeam.Fifthists], CTeamEscapeTarget.CustomRole(CRoleTypeId.FifthistConvert));
        yield return DefineTeamRule("FoundationEscortGoC", 100, CTeam.FoundationForces, [CTeam.GoC], CTeamEscapeTarget.CustomRole(CRoleTypeId.GoCOperative));
        yield return DefineTeamRule("GuardsEscortInsurgency", 100, CTeam.Guards, InsurgencyTeams, CTeamEscapeTarget.VanillaRole(RoleTypeId.ChaosConscript));
        yield return DefineTeamRule("GuardsEscortFifthists", 100, CTeam.Guards, [CTeam.Fifthists], CTeamEscapeTarget.CustomRole(CRoleTypeId.FifthistConvert));
        yield return DefineTeamRule("GuardsEscortGoC", 100, CTeam.Guards, [CTeam.GoC], CTeamEscapeTarget.CustomRole(CRoleTypeId.GoCOperative));

        yield return DefineTeamRule("FifthistsEscortInsurgency", 100, CTeam.Fifthists, InsurgencyTeams, CTeamEscapeTarget.VanillaRole(RoleTypeId.ChaosConscript));
        yield return DefineTeamRule("FifthistsEscortFoundation", 100, CTeam.Fifthists, FoundationTeams, CTeamEscapeTarget.VanillaRole(RoleTypeId.NtfPrivate));
        yield return DefineTeamRule("FifthistsEscortGoC", 100, CTeam.Fifthists, [CTeam.GoC], CTeamEscapeTarget.CustomRole(CRoleTypeId.GoCOperative));
    }

    private static readonly CTeam[] FoundationTeams =
    [
        CTeam.FoundationForces,
        CTeam.Scientists,
        CTeam.Guards
    ];

    private static readonly CTeam[] InsurgencyTeams =
    [
        CTeam.ChaosInsurgency,
        CTeam.ClassD
    ];
}
