using System.Collections.Generic;
using Exiled.API.Enums;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.CustomMaps;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class RrhAegis : CRole
{
    protected override string RoleName { get; set; } = $"<color={ServerColors.Red}><b>Red Right Hand: Aegis</b></color>";
    protected override string Description { get; set; } = $"Alpha-1の護衛担当。\n要員を守りながら、部隊の作戦継続を支援せよ。\n<color={ServerColors.Crimson}>高位の職員</color>を脱出させろ。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.RrhAegis;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "RrhAegis";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfSergeant;
    protected override float? SpawnMaxHealth => 140f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.KeycardMTFCaptain,
        ItemType.GunCrossvec,
        ItemType.GrenadeFlash,
        ItemType.Adrenaline,
        ItemType.Adrenaline,
        ItemType.Medkit,
        ItemType.Radio,
        typeof(ArmorVip),
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato9] = 200,
    };
    protected override Vector3? SpawnPosition => MapFlags.FirstTeamSpawnPoint;
    protected override string SpawnCustomInfo => $"<color={ServerColors.Red}>Red Right Hand Aegis</color>";
}
