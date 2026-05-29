using System.Collections.Generic;
using Exiled.API.Enums;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class RrhEnforcer : CRole
{
    protected override string RoleName { get; set; } = $"<color={ServerColors.Red}><b>Red Right Hand: Enforcer</b></color>";
    protected override string Description { get; set; } = $"Alpha-1の執行官。\n敵対勢力を迅速に排除し、機動部隊の前進を支援せよ。\n<color={ServerColors.Crimson}>高位の職員</color>を脱出させろ。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.RrhEnforcer;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "RrhEnforcer";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfSergeant;
    protected override float? SpawnMaxHealth => 120f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.KeycardMTFCaptain,
        ItemType.GunE11SR,
        ItemType.GrenadeHE,
        ItemType.Adrenaline,
        ItemType.Medkit,
        ItemType.Radio,
        ItemType.ArmorHeavy,
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato556] = 220,
    };
    protected override UnityEngine.Vector3? SpawnPosition => MapFlags.FirstTeamSpawnPoint;
    protected override string SpawnCustomInfo => $"<color={ServerColors.Red}>Red Right Hand Enforcer</color>";
}
