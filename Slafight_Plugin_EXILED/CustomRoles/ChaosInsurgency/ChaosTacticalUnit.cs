using System.Collections.Generic;
using Exiled.API.Enums;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

namespace Slafight_Plugin_EXILED.CustomRoles.ChaosInsurgency;

public class ChaosTacticalUnit : CRole
{
    protected override string RoleName { get; set; } = "カオス・インサージェンシー 戦術兵";
    protected override string Description { get; set; } = "特殊なリボルバーを用いて邪魔者を排除せよ！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.ChaosTacticalUnit;
    protected override CTeam Team { get; set; } = CTeam.ChaosInsurgency;
    protected override string UniqueRoleKey { get; set; } = "ChaosTacticalUnit";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.ChaosMarauder;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.KeycardChaosInsurgency,
        ItemType.Medkit,
        ItemType.Painkillers,
        ItemType.ArmorCombat,
        ItemType.GrenadeFlash,
        typeof(GunTacticalRevolver),
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Ammo44Cal] = 40,
    };
    protected override string SpawnCustomInfo => "Chaos Insurgency Tactical Unit";

}
