using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.Fifthist;

public class FifthistRescure : CRole
{
    protected override string RoleName { get; set; } = "第五教会 救出師";
    protected override string Description { get; set; } = "非常に<color=#ff00fa>第五的</color>な存在を脱出させなければいけない";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.FifthistRescure;
    protected override CTeam Team { get; set; } = CTeam.Fifthists;
    protected override string UniqueRoleKey { get; set; } = "FIFTHIST";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Tutorial;
    protected override float? SpawnMaxHealth => 135f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.GunAK,
        ItemType.ArmorHeavy,
        typeof(KeycardFifthist),
        ItemType.Medkit,
        ItemType.Adrenaline,
        ItemType.SCP500,
        ItemType.GrenadeHE,
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato762] = 180,
    };
    protected override Vector3? SpawnPosition => new Vector3(124f, 289f, 21f);
    protected override string SpawnCustomInfo => "<color=#FF0090>Fifthist Rescure</color>";
}
