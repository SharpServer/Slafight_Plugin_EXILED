using System.Collections.Generic;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Objects;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// 1 モード分の初期ロールテーブルと上限を持つコンテキスト。
/// </summary>
public abstract class RoleTableContext
{
    public abstract string Name { get; }

    public abstract List<WeightedRoleEntry> ScpRoles { get; }

    public abstract List<WeightedRoleEntry> ScientistRoles { get; }

    public abstract List<WeightedRoleEntry> GuardRoles { get; }

    public abstract List<WeightedRoleEntry> ClassDRoles { get; }

    public abstract List<RoleLimitEntry> Limits { get; }

    public RoleTablePool Tables => new(ScpRoles, ScientistRoles, GuardRoles, ClassDRoles);

    public RoleLimitPool LimitPool => new(Limits);

    protected static WeightedRoleEntry W(object role, float weight = 1.0f)
        => new(role, weight);

    protected static RoleLimitEntry L(object role, int limit)
        => new(role, limit);

    protected static List<WeightedRoleEntry> DefaultScpRoles()
        =>
        [
            W(RoleTypeId.Scp173, 1.15f),
            W(RoleTypeId.Scp049, 1.08f),
            W(RoleTypeId.Scp079, 1.05f),
            W(RoleTypeId.Scp096, 0.85f),
            W(RoleTypeId.Scp106, 1.1f),
            W(RoleTypeId.Scp939, 1.1f),
            W(RoleTypeId.Scp3114, 0.95f),
            W(CRoleTypeId.Scp3005, 1.05f),
            W(CRoleTypeId.Scp966, 1.05f),
            W(CRoleTypeId.Scp682, 0.8f),
            W(CRoleTypeId.Scp035, 0.8f),
            W(CRoleTypeId.Scp999, 0.77f),
            W(CRoleTypeId.Scp610, 1.0264f)
        ];

    protected static List<WeightedRoleEntry> DefaultScientistRoles()
        =>
        [
            W(RoleTypeId.Scientist),
            W(CRoleTypeId.ZoneManager),
            W(CRoleTypeId.FacilityManager),
            W(CRoleTypeId.Engineer),
            W(CRoleTypeId.SiteNavigator),
            W(CRoleTypeId.ObjectObserver)
        ];

    protected static List<WeightedRoleEntry> DefaultGuardRoles()
        =>
        [
            W(RoleTypeId.FacilityGuard),
            W(CRoleTypeId.EvacuationGuard),
            W(CRoleTypeId.SecurityChief),
            W(CRoleTypeId.ChamberGuard),
            W(CRoleTypeId.SupplyManager),
        ];

    protected static List<WeightedRoleEntry> DefaultClassDRoles()
        =>
        [
            W(RoleTypeId.ClassD),
            W(CRoleTypeId.Janitor),
            W(CRoleTypeId.Bloodfiend, 0.85f),
            W(CRoleTypeId.ChaosUndercoverAgent)
        ];

    protected static List<RoleLimitEntry> DefaultLimits()
        =>
        [
            L(CRoleTypeId.Janitor, 3),
            L(CRoleTypeId.Bloodfiend, 1),
            L(CRoleTypeId.ChaosUndercoverAgent, 1),

            L(CRoleTypeId.ZoneManager, 2),
            L(CRoleTypeId.FacilityManager, 1),
            L(CRoleTypeId.SiteNavigator, 1),
            L(CRoleTypeId.ObjectObserver, 1),

            L(CRoleTypeId.EvacuationGuard, 1),
            L(CRoleTypeId.SecurityChief, 1),
            L(CRoleTypeId.ChamberGuard, 1),
            L(CRoleTypeId.SupplyManager, 2),

            L(CRoleTypeId.Scp682, 1)
        ];
}
