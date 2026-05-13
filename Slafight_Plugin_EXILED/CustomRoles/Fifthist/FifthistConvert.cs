using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.Fifthist;

public class FifthistConvert : CRole
{
    protected override string RoleName { get; set; } = "<color=#ff5ffa>第五教会 改宗者</color>";
    protected override string Description { get; set; } = "貴方は新たに第五教会に加わった。全てを第五に捧げるのです。\nSCP-1425を使って、更に第五を広めろ！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.FifthistConvert;
    protected override CTeam Team { get; set; } = CTeam.Fifthists;
    protected override string UniqueRoleKey { get; set; } = "FifthistConvert";

    public override void SpawnRole(Player? player,RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.Tutorial);
        Vector3 offset;
        int maxHealth = 150;

        player.UniqueRole = UniqueRoleKey;
        player.SetCustomInfo("<color=#FF0090>Fifthist Convert</color>");
        player.MaxHealth = maxHealth;
        player.Health = maxHealth;
            
        Room spawnRoom = Room.Get(RoomType.Surface);
        Log.Debug(spawnRoom.Position);
        offset = new Vector3(0f,0f,0f);
        player.Position = new Vector3(124f,289f,21f);//SpawnRoom.Position + SpawnRoom.Rotation * offset;
        //player.Rotation = SpawnRoom.Rotation;
            
        player.ClearInventory();
        Log.Debug("Giving Items to FifthistConvert");
        player.AddItem(ItemType.GunA7);
        //player.AddItem(ItemType.KeycardFacilityManager);
        player.AddItem(ItemType.Medkit);
        player.AddItem(ItemType.ArmorCombat);
        CItem.Get<KeycardFifthist>()?.Give(player);
        CItem.Get<Scp1425>()?.Give(player);
        player.SetAmmo(AmmoType.Nato762,170);
    }
}