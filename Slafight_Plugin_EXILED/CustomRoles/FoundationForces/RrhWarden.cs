using System.Collections.Generic;
using Exiled.API.Enums;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.CustomMaps;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class RrhWarden : CRole
{
    protected override string RoleName { get; set; } = $"<color={ServerColors.Red}><b>Red Right Hand: Warden</b></color>";
    protected override string Description { get; set; } = $"Alpha-1の監督官。\nO5の意思として現場を制圧し、財団側の勝利を確実にせよ。\n<color={ServerColors.Crimson}>高位の職員</color>を脱出させろ。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.RrhWarden;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "RrhWarden";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfCaptain;
    protected override float? SpawnMaxHealth => 130f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.KeycardMTFCaptain,
        ItemType.GrenadeHE,
        ItemType.GrenadeFlash,
        ItemType.Adrenaline,
        ItemType.Medkit,
        ItemType.Radio,
        typeof(ArmorVip),
        typeof(GunFRMGX),
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato556] = 260,
    };
    protected override UnityEngine.Vector3? SpawnPosition => MapFlags.FirstTeamSpawnPoint;
    protected override string SpawnCustomInfo => $"<color={ServerColors.Red}>Red Right Hand Warden</color>";
}
