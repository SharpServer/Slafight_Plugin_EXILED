using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
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
    protected override UnityEngine.Vector3? SpawnPosition => MapFlags.FirstTeamSpawnPoint;
    protected override string SpawnCustomInfo => $"<color={ServerColors.Red}>Red Right Hand Aegis</color>";
}

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
    protected override UnityEngine.Vector3? SpawnPosition => MapFlags.FirstTeamSpawnPoint;
    protected override string SpawnCustomInfo => $"<color={ServerColors.Red}>Red Right Hand Assaulter</color>";
}
