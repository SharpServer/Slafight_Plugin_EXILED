using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.CustomRoles.SCPs;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.SpawnSets.RoundStart;

public class FirstRolesHumanSet : SpawnSet
{
    public override string Name => "First Spawn Human Roles";
    public override IReadOnlyList<SpawnSetRoleDefinition> SpawnRoles =>
    [
        SpawnSetRoleDefinition.Vanilla(RoleTypeId.ClassD, count: 99),
        SpawnSetRoleDefinition.Vanilla(RoleTypeId.Scientist, count: 99),
        SpawnSetRoleDefinition.Vanilla(RoleTypeId.FacilityGuard, count: 99),
    ];
}

public class FirstRolesSCPsSet : SpawnSet
{
    public override string Name => "First Spawn SCPs Roles";
    public override int AllowedPlayerCount => Player.List.Count(p => p.IsSafePlayer()) switch
    {
        < 4 => 1,
        < 8 => 2,
        < 12 => 3,
        _ => 4,
    };

    public override IReadOnlyList<SpawnSetRoleDefinition> SpawnRoles =>
    [
        // 重みは master の RoleTableContext.DefaultScpRoles() に準拠。
        // SCP は 1 体につき 1 人なので count はすべて 1。
        SpawnSetRoleDefinition.Custom<Scp173>(count: 1, weight: 1.15f),
        SpawnSetRoleDefinition.Custom<Scp106>(count: 1, weight: 1.1f),
        SpawnSetRoleDefinition.Custom<Scp939>(count: 1, weight: 1.1f),
        SpawnSetRoleDefinition.Custom<Scp049>(count: 1, weight: 1.08f),
        // 079 は単独だと何もできないので 2 体目以降から。
        SpawnSetRoleDefinition.Custom<Scp079>(count: 1, weight: 1.05f).AfterSpawned(1),
        SpawnSetRoleDefinition.Custom<Scp3114>(count: 1, weight: 0.95f),
        SpawnSetRoleDefinition.Custom<Scp096>(count: 1, weight: 0.85f),
    ];
}