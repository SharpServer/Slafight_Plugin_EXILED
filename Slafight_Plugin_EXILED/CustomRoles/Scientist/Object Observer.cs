using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features.Doors;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.Scientist;

public class ObjectObserver : CRole
{
    protected override string RoleName { get; set; } = "オブジェクト観測者";
    protected override string Description { get; set; } = "SCPオブジェクトの状況を監視し、報告する。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.ObjectObserver;
    protected override CTeam Team { get; set; } = CTeam.Scientists;
    protected override string UniqueRoleKey { get; set; } = "ObjectObserver";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Scientist;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.KeycardScientist,
        ItemType.Medkit,
        ItemType.ArmorLight,
    ];
    protected override Vector3? SpawnPosition => Door.Get(DoorType.Scp173Connector).Position + new Vector3(0f, 0.35f, 0f);
    protected override string SpawnCustomInfo => "Object Observer";
}
