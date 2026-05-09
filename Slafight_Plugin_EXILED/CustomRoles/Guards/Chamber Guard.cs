using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.Guards;

public class ChamberGuard : CRole
{
    protected override string RoleName { get; set; } = "収容室警備";
    protected override string Description { get; set; } = "Dクラス職員やオブジェクトの異常を監視する。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.ChamberGuard;
    protected override CTeam Team { get; set; } = CTeam.Guards;
    protected override string UniqueRoleKey { get; set; } = "ChamberGuard";

    public override void SpawnRole(Player? player,RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.FacilityGuard);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 100;
        player.Health = player.MaxHealth;
        player.ClearInventory();
        Log.Debug("Giving Items to ChamberGuard");
        player.AddItem(ItemType.GunFSP9);
        player.AddItem(ItemType.KeycardGuard);
        player.AddItem(ItemType.Medkit);
        player.AddItem(ItemType.ArmorLight);
        player.AddItem(ItemType.Radio);
        player.AddAmmo(AmmoType.Nato9,100);
        var pos = Door.Get(DoorType.Scp173Connector).Position;
        pos += new Vector3(0f,0.35f,0f);
        player.Position = pos;
        Log.Debug($"RoomPos: {pos},CGuard pos: {player.Position}");
            
        player.SetCustomInfo("Chamber Guard");
    }
}