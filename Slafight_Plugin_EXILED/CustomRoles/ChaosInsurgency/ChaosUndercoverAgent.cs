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

    public override void SpawnRole(Player? player, RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.ChaosMarauder);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 100;
        player.Health = player.MaxHealth;
        
        player.ClearInventory();
        player.AddItem(ItemType.GrenadeFlash);
        player.AddItem(ItemType.Medkit);
        player.AddItem(ItemType.Adrenaline);
        player.AddItem(ItemType.ArmorCombat);
        if (!player.HasItem(ItemType.GunRevolver))
            player.AddItem(ItemType.GunRevolver);
        CItem.Get<KeycardConscripts>()?.Give(player); // Conscripts Card
        CItem.Get<CUA_SpyKit>()?.Give(player);
        
        player.SetAmmo(AmmoType.Ammo44Cal, 24);
            
        player.SetCustomInfo("Chaos Insurgency Undercover Agent");
        Timing.CallDelayed(0.05f, () =>
        {
            var data = StaticUtils.GetWorldFromRoomLocal(RoomType.HczCrossRoomWater, new Vector3(-4.98f, -9.25f, 2.3f), new Vector3(0f, 270f, 0f));
            player.Position = data.worldPosition;
            player.Rotation = data.worldRotation;
        });
    }
}