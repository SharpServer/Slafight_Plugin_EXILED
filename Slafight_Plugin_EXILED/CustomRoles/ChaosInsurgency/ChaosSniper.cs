using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.CustomRoles.ChaosInsurgency;

public class ChaosSniper : CRole
{
    protected override string RoleName { get; set; } = "カオス・インサージェンシー 狙撃兵";
    protected override string Description { get; set; } = "スナイパーライフルを用いて素早く対象を制圧する。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.ChaosSniper;
    protected override CTeam Team { get; set; } = CTeam.ChaosInsurgency;
    protected override string UniqueRoleKey { get; set; } = "ChaosSniper";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.ChaosRepressor;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.KeycardChaosInsurgency,
        ItemType.Medkit,
        ItemType.Adrenaline,
        ItemType.ArmorCombat,
        typeof(GunSL8),
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato556] = 100,
    };
    protected override string SpawnCustomInfo => "Chaos Insurgency Sniper";

}
