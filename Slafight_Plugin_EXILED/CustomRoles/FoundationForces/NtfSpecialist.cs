using System.Collections.Generic;
using Exiled.API.Enums;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class NtfSpecialist : CRole
{
    protected override string RoleName { get; set; } = "九尾狐 スペシャリスト";
    protected override string Description { get; set; } = "九尾狐の中でもとてもオブジェクト達に精通している戦術スペシャリスト。\n専用の対物ライフルでオブジェクトを無力化する。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.NtfSpecialist;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "NtfSpecialist";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfSpecialist;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.GunCOM18,
        ItemType.KeycardMTFCaptain,
        ItemType.Medkit,
        ItemType.Medkit,
        ItemType.GrenadeHE,
        ItemType.ArmorHeavy,
        ItemType.Radio,
        typeof(GunM82),
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato556] = 180,
        [AmmoType.Nato9] = 120,
    };
    protected override string SpawnCustomInfo => "Nine-tailed Fox Specialist";

}
