using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Objects;

namespace Slafight_Plugin_EXILED.API.Features.RoleTableDictionaries.Contexts;

public sealed class NormalRoleTableContext : RoleTableContext
{
    public const string ContextName = "Normal";

    public override string Name => ContextName;

    public override List<WeightedRoleEntry> ScpRoles { get; } = DefaultScpRoles();

    public override List<WeightedRoleEntry> ScientistRoles { get; } = DefaultScientistRoles();

    public override List<WeightedRoleEntry> GuardRoles { get; } = DefaultGuardRoles();

    public override List<WeightedRoleEntry> ClassDRoles { get; } = DefaultClassDRoles();

    public override List<RoleLimitEntry> Limits { get; } = DefaultLimits();
}
