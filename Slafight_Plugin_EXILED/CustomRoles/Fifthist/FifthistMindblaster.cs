using System.Collections.Generic;
using Exiled.API.Enums;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.Fifthist;

public class FifthistMindblaster : CRole
{
    protected override string RoleName { get; set; } = "第五教会 思壊師";
    protected override string Description { get; set; } = $"<color={CTeam.Fifthists.GetTeamColor()}>第五の力</color>を用いて敵の思念を破壊する";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.FifthistMindblaster;
    protected override CTeam Team { get; set; } = CTeam.Fifthists;
    protected override string UniqueRoleKey { get; set; } = "FifthistMindblaster";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Tutorial;
    protected override float? SpawnMaxHealth => 155f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.GunAK,
        ItemType.ArmorLight,
        typeof(KeycardFifthist),
        typeof(Mindblaster),
        ItemType.Medkit,
        ItemType.GrenadeHE,
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato762] = 80,
    };

    protected override Vector3? SpawnPosition => PositionProvider.GetExitBPosition();
    protected override string SpawnCustomInfo => "<color=#FF0090>Fifthist Mindblaster</color>";
}
