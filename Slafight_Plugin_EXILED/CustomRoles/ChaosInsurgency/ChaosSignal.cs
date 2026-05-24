using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

namespace Slafight_Plugin_EXILED.CustomRoles.ChaosInsurgency;

public class ChaosSignal : CRole
{
    protected override string RoleName { get; set; } = "カオス・インサージェンシー 通信兵";
    protected override string Description { get; set; } = "S-Nav 300を用いてユニークな部屋を捜索する。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.ChaosSignal;
    protected override CTeam Team { get; set; } = CTeam.ChaosInsurgency;
    protected override string UniqueRoleKey { get; set; } = "ChaosSignal";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.ChaosRifleman;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.KeycardChaosInsurgency,
        ItemType.Medkit,
        ItemType.Painkillers,
        ItemType.ArmorCombat,
        ItemType.GunAK,
        typeof(SNAV300),
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato762] = 120,
    };
    protected override string SpawnCustomInfo => "Chaos Insurgency Signal";

    protected override void OnRoleSpawnStarting(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        player.SetCategoryLimit(ItemCategory.Radio, 2);
    }
}
