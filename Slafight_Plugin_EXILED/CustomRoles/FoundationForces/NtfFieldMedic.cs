using System.Collections.Generic;
using Exiled.API.Enums;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class NtfFieldMedic : CRole
{
    protected override string RoleName { get; set; } = "NTF FIELD MEDIC";
    protected override string Description { get; set; } =
        "Nine-Tailed-Foxの野戦衛生兵。\n" +
        "S-41 MEDICAL PISTOLで遠距離から味方を治療できる。\n" +
        "敵勢力も回復してしまうため射線に注意。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.NtfFieldMedic;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "NtfFieldMedic";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfPrivate;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.GunCrossvec,
        ItemType.KeycardMTFOperative,
        ItemType.ArmorCombat,
        ItemType.Radio,
        ItemType.Flashlight,
        typeof(S41MedicalPistol),
        typeof(AdvancedMedkit),
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato9] = 120,
    };
    protected override string SpawnCustomInfo => "Nine-tailed Fox Field Medic";

}
