using System.Collections.Generic;
using Exiled.API.Enums;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class HdCommander : CRole
{
    protected override string RoleName { get; set; } = "<color=#252525>ハンマーダウン 指揮官</color>";
    protected override string Description { get; set; } = "Nu-7の歩兵たちを指揮し、制圧を進める。\n偉大なる我らが元帥の指示に従え！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.HdCommander;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "HdCommander";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfSergeant;
    protected override float? SpawnMaxHealth => 125f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.KeycardMTFOperative,
        ItemType.Adrenaline,
        ItemType.Medkit,
        ItemType.GrenadeHE,
        ItemType.GrenadeHE,
        ItemType.Radio,
        typeof(ArmorVip),
        typeof(GunN7CR),
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato556] = 200,
    };
    protected override string SpawnCustomInfo => "<color=#727472>Hammer Down Commander</color>";

}
