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

    public override void SpawnRole(Player? player,RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.FacilityGuard);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 100;
        player.Health = player.MaxHealth;
        player.ClearInventory();
        Log.Debug("Giving Items to EvacuationGuard");
        player.AddItem(ItemType.GunFSP9);
        player.AddItem(ItemType.KeycardGuard);
        player.AddItem(ItemType.Medkit);
        player.AddItem(ItemType.Painkillers);
        player.AddItem(ItemType.ArmorCombat);
        player.AddItem(ItemType.Radio);
        player.SetAmmo(AmmoType.Nato9,150);
        var pos = Room.Get(RoomType.LczArmory).WorldPosition(new Vector3(0f,1f,0f));
        player.Position = pos;
        Log.Debug($"RoomPos: {pos},EvacuationManager pos: {player.Position}");
            
        CustomInfoDisplay.Apply(player, "Emergency Evacuation Guard");
    }
}
