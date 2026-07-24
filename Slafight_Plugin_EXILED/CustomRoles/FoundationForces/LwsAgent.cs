using System.Collections.Generic;
using Exiled.API.Enums;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class LwsAgent : CRole
{
    protected override string RoleName { get; set; } = $"<color={ServerColors.Silver}><b>Law's Left Hand: Agent</b></color>";
    protected override string Description { get; set; } = "Omega-1の実働員。\n命令に従い、施設の秩序回復を支援せよ。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.LwsAgent;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "LwsAgent";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfPrivate;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.KeycardMTFOperative,
        ItemType.GunFSP9,
        ItemType.GrenadeFlash,
        ItemType.Medkit,
        ItemType.Radio,
        ItemType.ArmorCombat,
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato9] = 140,
    };
    protected override Vector3? SpawnPosition => MapFlags.FirstTeamSpawnPoint;
    protected override string SpawnCustomInfo => $"<color={ServerColors.Silver}>Law's Left Hand Agent</color>";
}
