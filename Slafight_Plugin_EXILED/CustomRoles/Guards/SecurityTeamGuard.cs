using System.Collections.Generic;
using Exiled.API.Enums;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.CustomMaps;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.Guards;

public class SecurityTeamGuard : CRole
{
    protected override string RoleName { get; set; } = "保安部隊員";
    protected override string Description { get; set; } = "職員たちを保護し、脱出を助ける。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.SecurityTeamGuard;
    protected override CTeam Team { get; set; } = CTeam.Guards;
    protected override string UniqueRoleKey { get; set; } = "SecurityTeamGuard";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.FacilityGuard;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.KeycardGuard,
        ItemType.Medkit,
        ItemType.Painkillers,
        ItemType.ArmorCombat,
        ItemType.Radio,
        typeof(GunFSP18),
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato9] = 110,
    };
    protected override Vector3? SpawnPosition => MapFlags.FirstTeamSpawnPoint;
    protected override string SpawnCustomInfo => "Security Team Guard";

}
