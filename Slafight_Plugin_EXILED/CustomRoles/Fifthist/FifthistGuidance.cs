using System.Collections.Generic;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.Fifthist;

public class FifthistGuidance : CRole
{
    protected override string RoleName { get; set; } = "第五教会 案内人";
    protected override string Description { get; set; } = "第五主義を広め、人々を第五世界へと誘う案内人。\n杖を使って相手を第五すると第五主義者に出来る。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.FifthistGuidance;
    protected override CTeam Team { get; set; } = CTeam.Fifthists;
    protected override string UniqueRoleKey { get; set; } = "FifthistGuidance";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Tutorial;
    protected override float? SpawnMaxHealth => 125f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        typeof(CaneOfTheStars),
        ItemType.ArmorHeavy,
        typeof(KeycardFifthist),
        ItemType.Medkit,
        ItemType.Adrenaline,
        ItemType.SCP500,
        ItemType.GrenadeHE,
    ];
    protected override Vector3? SpawnPosition => new Vector3(124f, 289f, 21f);
    protected override string SpawnCustomInfo => "<color=#FF0090>Fifthist Guidance</color>";
}
