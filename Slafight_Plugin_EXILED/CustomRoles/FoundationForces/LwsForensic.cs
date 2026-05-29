using System.Collections.Generic;
using Exiled.API.Enums;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.CustomMaps;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

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
