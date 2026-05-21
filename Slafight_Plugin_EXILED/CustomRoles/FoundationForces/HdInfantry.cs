using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class HdInfantry : CRole
{
    protected override string RoleName { get; set; } = "<color=#353535>ハンマーダウン 歩兵</color>";
    protected override string Description { get; set; } = "Nu-7の最下級兵だが、それでも強い装備が持たされている。\nNu-7とはこういう奴らなのだ";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.HdInfantry;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "HdInfantry";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfPrivate;
    protected override float? SpawnMaxHealth => 110f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.GunCrossvec,
        ItemType.KeycardMTFOperative,
        ItemType.Adrenaline,
        ItemType.Medkit,
        ItemType.GrenadeFlash,
        ItemType.GrenadeHE,
        ItemType.Radio,
        typeof(ArmorInfantry),
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato9] = 140,
    };
    protected override string SpawnCustomInfo => "<color=#727472>Hammer Down Infantry</color>";

}
