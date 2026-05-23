using System.Collections.Generic;
using Exiled.API.Enums;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.CustomMaps;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class LwsJudgement : CRole
{
    protected override string RoleName { get; set; } = $"<color={ServerColors.Silver}><b>Law's Left Hand: Judgement</b></color>";
    protected override string Description { get; set; } = "Omega-1を率いる裁定担当。\n財団の秩序を回復し、敵対勢力を排除せよ。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.LwsJudgement;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "LwsJudgement";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfCaptain;
    protected override float? SpawnMaxHealth => 120f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.KeycardMTFCaptain,
        ItemType.GunE11SR,
        ItemType.GrenadeHE,
        ItemType.GrenadeFlash,
        ItemType.Adrenaline,
        ItemType.Medkit,
        ItemType.Radio,
        typeof(ArmorVip),
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato556] = 220,
        [AmmoType.Nato9] = 80,
    };
    protected override UnityEngine.Vector3? SpawnPosition => MapFlags.FirstTeamSpawnPoint;
    protected override string SpawnCustomInfo => $"<color={ServerColors.Silver}>Law's Left Hand Judgement</color>";
}

public class LwsLiaison : CRole
{
    protected override string RoleName { get; set; } = $"<color={ServerColors.Silver}><b>Law's Left Hand: Liaison</b></color>";
    protected override string Description { get; set; } = "Omega-1の連絡官。\n現場の味方と連携し、重要人物の保護と敵対勢力の排除を遂行せよ。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.LwsLiaison;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "LwsLiaison";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfSergeant;
    protected override float? SpawnMaxHealth => 110f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.KeycardMTFCaptain,
        ItemType.GunCrossvec,
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
    protected override string SpawnCustomInfo => $"<color={ServerColors.Silver}>Law's Left Hand Liaison</color>";
}

public class LwsForensic : CRole
{
    protected override string RoleName { get; set; } = $"<color={ServerColors.Silver}><b>Law's Left Hand: Forensic</b></color>";
    protected override string Description { get; set; } = "Omega-1の調査官。\n施設内の脅威を見極め、部隊の生存を支援せよ。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.LwsForensic;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "LwsForensic";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfSpecialist;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.KeycardMTFOperative,
        ItemType.GunCOM18,
        ItemType.Flashlight,
        ItemType.Medkit,
        ItemType.Adrenaline,
        ItemType.Radio,
        ItemType.ArmorCombat,
        typeof(S41MedicalPistol),
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato9] = 120,
    };
    protected override UnityEngine.Vector3? SpawnPosition => MapFlags.FirstTeamSpawnPoint;
    protected override string SpawnCustomInfo => $"<color={ServerColors.Silver}>Law's Left Hand Forensic</color>";
}

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
    protected override UnityEngine.Vector3? SpawnPosition => MapFlags.FirstTeamSpawnPoint;
    protected override string SpawnCustomInfo => $"<color={ServerColors.Silver}>Law's Left Hand Agent</color>";
}
