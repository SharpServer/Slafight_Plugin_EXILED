using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.Scientist;

public class ZoneManager : CRole
{
    protected override string RoleName { get; set; } = "区画管理官";
    protected override string Description { get; set; } = "各区画に割り当てられた軽度な権限をもつ科学者";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.ZoneManager;
    protected override CTeam Team { get; set; } = CTeam.Scientists;
    protected override string UniqueRoleKey { get; set; } = "ZoneManager";

    public override void SpawnRole(Player? player,RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.Scientist);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 100;
        player.Health = player.MaxHealth;
        player.ClearInventory();
        Log.Debug("Giving Items to ZoneManager");
        player.AddItem(ItemType.GunFSP9);
        player.AddItem(ItemType.KeycardZoneManager);
        player.AddItem(ItemType.KeycardScientist);
        player.AddItem(ItemType.Medkit);
        player.AddItem(ItemType.ArmorLight);
        player.AddItem(ItemType.Radio);
        var selectZone = Random.Range(0,2);
        switch (selectZone)
        {
            case 0:
            {
                var pos = Room.Get(RoomType.LczCafe).WorldPosition(new Vector3(0f,1f,0f));
                player.Position = pos;
                Log.Debug($"RoomPos: {pos},ZoneManager pos: {player.Position}");
                break;
            }
            case 1:
            {
                var pos = Room.Get(RoomType.HczHid).WorldPosition(new Vector3(0f,1f,0f));
                player.Position = pos;
                Log.Debug($"RoomPos: {pos},ZoneManager pos: {player.Position}");
                break;
            }
        }
            
        player.SetCustomInfo("Zone Manager");
    }
}