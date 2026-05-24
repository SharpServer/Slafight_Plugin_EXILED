using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features.Doors;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.ClassD;

public class Janitor : CRole
{
    protected override string RoleName { get; set; } = "<color=#ee7600>用務員</color>";
    protected override string Description { get; set; } = "特殊グレネードで近くの汚れを清掃できる";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Janitor;
    protected override CTeam Team { get; set; } = CTeam.ClassD;
    protected override string UniqueRoleKey { get; set; } = "Janitor";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.ClassD;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        typeof(FakeGrenade),
        typeof(FakeGrenade),
        typeof(FakeGrenade),
        typeof(FakeGrenade),
        typeof(FakeGrenade),
        typeof(FakeGrenade),
        ItemType.KeycardJanitor,
        ItemType.Radio,
    ];
    protected override Vector3? SpawnPosition => Door.Get(DoorType.Scp173Connector).Position + new Vector3(0f, 0.35f, 0f);
    protected override string SpawnCustomInfo => "Janitor";
}
