using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.ChaosInsurgency;

public class ChaosUndercoverAgent : CRole
{
    protected override string RoleName { get; set; } = "カオス・インサージェンシー 潜入工作員";
    protected override string Description { get; set; } = "施設に潜入した先遣隊。施設の偵察や略奪を行え！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.ChaosUndercoverAgent;
    protected override CTeam Team { get; set; } = CTeam.ChaosInsurgency;
    protected override string UniqueRoleKey { get; set; } = "ChaosUndercoverAgent";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.ChaosMarauder;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.GrenadeFlash,
        ItemType.Medkit,
        ItemType.Adrenaline,
        ItemType.ArmorCombat,
        typeof(KeycardConscripts),
        typeof(CuaSpyKit),
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Ammo44Cal] = 24,
    };
    protected override string SpawnCustomInfo => "Chaos Insurgency Undercover Agent";

    protected override void GiveCustomItems(Player player)
    {
        if (!player.HasItem(ItemType.GunRevolver))
            player.AddItem(ItemType.GunRevolver);
    }

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        var playerId = player.Id;

        Timing.CallDelayed(RoleSpawnTimings.AfterRoleSet, () =>
        {
            var current = Player.Get(playerId);
            if (!Check(current))
                return;

            var data = StaticUtils.GetWorldFromRoomLocal(RoomType.HczCrossRoomWater, new Vector3(-4.98f, -9.25f, 2.3f), new Vector3(0f, 270f, 0f));
            if (TrySetPlayerPosition(current, data.worldPosition, nameof(ChaosUndercoverAgent)))
                current.Rotation = data.worldRotation;
        });
    }
}
