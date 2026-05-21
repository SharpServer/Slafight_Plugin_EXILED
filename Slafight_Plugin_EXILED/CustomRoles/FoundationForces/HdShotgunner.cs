using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class HdShotgunner : CRole
{
    protected override string RoleName { get; set; } = "<color=#353535>ハンマーダウン 砲弾兵</color>";
    protected override string Description { get; set; } = "ショットガンを二丁持ちしたNu-7の歩兵。\n" +
                                                          "素早い猛攻で敵を粉砕する。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.HdShotgunner;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "HdShotgunner";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfPrivate;
    protected override float? SpawnMaxHealth => 110f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.GunShotgun,
        ItemType.GunShotgun,
        ItemType.KeycardMTFOperative,
        ItemType.Adrenaline,
        ItemType.Medkit,
        ItemType.Radio,
        typeof(ArmorInfantry),
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Ammo12Gauge] = 200,
    };
    protected override string SpawnCustomInfo => "<color=#727472>Hammer Down Shotgunner</color>";

}
