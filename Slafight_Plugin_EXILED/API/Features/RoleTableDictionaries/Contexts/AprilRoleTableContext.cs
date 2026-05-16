using System.Collections.Generic;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Objects;

namespace Slafight_Plugin_EXILED.API.Features.RoleTableDictionaries.Contexts;

public sealed class AprilRoleTableContext : RoleTableContext
{
    public const string ContextName = "April";

    public override string Name => ContextName;

    public override List<WeightedRoleEntry> ScpRoles { get; } = DefaultScpRoles();

    public override List<WeightedRoleEntry> ScientistRoles { get; } =
    [
        W(RoleTypeId.Scientist),
        W(CRoleTypeId.ZoneManager),
        W(CRoleTypeId.FacilityManager),
        W(CRoleTypeId.Engineer),
        W(CRoleTypeId.SiteNavigator),
        W(CRoleTypeId.ObjectObserver),
        W(CRoleTypeId.CandyResearcher, 1.05f)
    ];

    public override List<WeightedRoleEntry> GuardRoles { get; } = DefaultGuardRoles();

    public override List<WeightedRoleEntry> ClassDRoles { get; } =
    [
        W(RoleTypeId.ClassD),
        W(CRoleTypeId.Janitor),
        W(CRoleTypeId.ChaosUndercoverAgent),
        W(CRoleTypeId.CandySubject, 1.05f)
    ];

    public override List<RoleLimitEntry> Limits { get; } =
    [
        L(CRoleTypeId.Janitor, 2),
        L(CRoleTypeId.ChaosUndercoverAgent, 1),
        L(CRoleTypeId.CandySubject, 2),

        L(CRoleTypeId.ZoneManager, 2),
        L(CRoleTypeId.FacilityManager, 1),
        L(CRoleTypeId.ObjectObserver, 1),
        L(CRoleTypeId.SiteNavigator, 1),
        L(CRoleTypeId.CandyResearcher, 2),

        L(CRoleTypeId.EvacuationGuard, 1),
        L(CRoleTypeId.SecurityChief, 1),
        L(CRoleTypeId.ChamberGuard, 1),

        L(CRoleTypeId.Scp682, 1)
    ];
}
