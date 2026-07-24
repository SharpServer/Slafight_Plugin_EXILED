using System.Collections.Generic;
using Exiled.API.Enums;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class RrhAssaulter : CRole
{
    protected override string RoleName { get; set; } = $"<color={ServerColors.Red}><b>Red Right Hand: Assaulter</b></color>";
    protected override string Description { get; set; } = $"Alpha-1の突入要員。\n強襲で敵の戦線を崩し、財団の優位を作れ。\n<color={ServerColors.Crimson}>高位の職員</color>を脱出させろ。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.RrhAssaulter;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "RrhAssaulter";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfPrivate;
    protected override float? SpawnMaxHealth => 110f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.KeycardMTFOperative,
        ItemType.GunCrossvec,
        ItemType.GrenadeHE,
        ItemType.GrenadeFlash,
        ItemType.Adrenaline,
        ItemType.Medkit,
        ItemType.Radio,
        ItemType.ArmorHeavy,
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato9] = 180,
    };
    protected override Vector3? SpawnPosition => MapFlags.FirstTeamSpawnPoint;
    protected override string SpawnCustomInfo => $"<color={ServerColors.Red}>Red Right Hand Assaulter</color>";
}
