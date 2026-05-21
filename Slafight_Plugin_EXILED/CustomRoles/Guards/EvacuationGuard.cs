using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.Guards;

public class EvacuationGuard : CRole
{
    protected override string RoleName { get; set; } = "下層避難支援警備隊員";
    protected override string Description { get; set; } = "下層の秩序を守り、職員の避難を助ける。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.EvacuationGuard;
    protected override CTeam Team { get; set; } = CTeam.Guards;
    protected override string UniqueRoleKey { get; set; } = "EvacuationGuard";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.FacilityGuard;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.GunFSP9,
        ItemType.KeycardGuard,
        ItemType.Medkit,
        ItemType.Painkillers,
        ItemType.ArmorCombat,
        ItemType.Radio,
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato9] = 150,
    };
    protected override Vector3? SpawnPosition => Room.Get(RoomType.LczArmory).WorldPosition(new Vector3(0f, 1f, 0f));
    protected override string SpawnCustomInfo => "Emergency Evacuation Guard";

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        Log.Debug($"RoomPos: {player.Position},EvacuationManager pos: {player.Position}");
    }
}
