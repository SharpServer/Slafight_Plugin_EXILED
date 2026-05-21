using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class HdDisarmer : CRole
{
    protected override string RoleName { get; set; } = "<color=#353535>ハンマーダウン 拘束兵</color>";
    protected override string Description { get; set; } = "当たると拘束できるスナイパーライフルを所持したNu-7の歩兵。\n敵の自由を奪い制圧を助ける。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.HdDisarmer;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "HdDisarmer";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfPrivate;
    protected override float? SpawnMaxHealth => 110f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        typeof(GunDisarmerRifle),
        ItemType.GunCrossvec,
        ItemType.KeycardMTFOperative,
        ItemType.Adrenaline,
        ItemType.Medkit,
        ItemType.Radio,
        typeof(ArmorInfantry),
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato9] = 120,
        [AmmoType.Nato556] = 200,
    };
    protected override string SpawnCustomInfo => "<color=#727472>Hammer Down Disarmer</color>";
}
