using System.Collections.Generic;
using Exiled.API.Enums;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class HdMarshal : CRole
{
    protected override string RoleName { get; set; } = "<color=#151515>ハンマーダウン 元帥</color>";
    protected override string Description { get; set; } = "Nu-7の師団を指揮し、勝利へと導く。\n敗北など許されない。突き進め！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.HdMarshal;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "HdMarshal";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfCaptain;
    protected override float? SpawnMaxHealth => 180f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.KeycardMTFCaptain,
        typeof(SerumC),
        typeof(AdvancedMedkit),
        ItemType.GrenadeHE,
        ItemType.GrenadeHE,
        ItemType.Radio,
        typeof(ArmorVip),
        typeof(GunN7Weltkrieg),
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato556] = 250,
    };
    protected override string SpawnCustomInfo => "<color=#727472>Hammer Down Marshal</color>";
}
