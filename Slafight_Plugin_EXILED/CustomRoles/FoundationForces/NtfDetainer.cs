using System.Collections.Generic;
using Exiled.API.Enums;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class NtfDetainer : CRole
{
    protected override string RoleName { get; set; } = "九尾狐 拘留兵";
    protected override string Description { get; set; } = "SCiPの行動阻害に特化したNTF特技兵。\n" +
                                                          "XE-11 ANOMALY DETAINERで対象の逃走を防ぐ。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.NtfDetainer;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "NtfDetainer";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfSergeant;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.GunFSP9,
        ItemType.KeycardMTFOperative,
        ItemType.ArmorCombat,
        ItemType.Medkit,
        ItemType.Radio,
        ItemType.Flashlight,
        typeof(GunAnomalyDetainer),
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato556] = 90,
        [AmmoType.Nato9] = 120,
    };
    protected override string SpawnCustomInfo => "Nine-tailed Fox Detainer";

}
