using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.CustomMaps;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.Guards;

public class SupplyManager : CRole
{
    protected override string RoleName { get; set; } = "施設供給管理官";
    protected override string Description { get; set; } = "施設の備品等の搬出入などを管理している職員。\n" +
                                                          "施設内に向かい警備員たちと合流せよ。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.SupplyManager;
    protected override CTeam Team { get; set; } = CTeam.Guards;
    protected override string UniqueRoleKey { get; set; } = "SupplyManager";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.FacilityGuard;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.Medkit,
        ItemType.Medkit,
        ItemType.ArmorLight,
        ItemType.Radio,
        ItemType.GunCOM18,
        typeof(KeycardSupplyManager),
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato9] = 80,
    };
    protected override string SpawnCustomInfo => "Supply Manager";

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        var playerId = player.Id;

        Timing.CallDelayed(RoleSpawnTimings.AfterRoleSet, () =>
        {
            var current = Player.Get(playerId);
            if (!Check(current))
                return;

            var position = Random.Range(0, 2) == 0
                ? MapFlags.SupplyManagerSpawnPointA
                : MapFlags.SupplyManagerSpawnPointB;

            TrySetPlayerPosition(current, position, nameof(SupplyManager));
        });
    }
}
