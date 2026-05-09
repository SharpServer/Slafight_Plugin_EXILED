using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.ClassD;

public class Janitor : CRole
{
    protected override string RoleName { get; set; } = "<color=#ee7600>用務員</color>";
    protected override string Description { get; set; } = "特殊グレネードで近くの汚れを清掃できる";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Janitor;
    protected override CTeam Team { get; set; } = CTeam.ClassD;
    protected override string UniqueRoleKey { get; set; } = "Janitor";

    public override void SpawnRole(Player? player,RoleSpawnFlags roleSpawnFlags)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.ClassD);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 100;
        player.Health = player.MaxHealth;
        player.ClearInventory();
        Log.Debug("Giving Items to Janitor");
        CItem.Get<FakeGrenade>()?.Give(player);
        CItem.Get<FakeGrenade>()?.Give(player);
        CItem.Get<FakeGrenade>()?.Give(player);
        CItem.Get<FakeGrenade>()?.Give(player);
        CItem.Get<FakeGrenade>()?.Give(player);
        CItem.Get<FakeGrenade>()?.Give(player);
        player.AddItem(ItemType.KeycardJanitor);
        player.AddItem(ItemType.Radio);
        var pos = Door.Get(DoorType.Scp173Connector).Position;
        pos += new Vector3(0f,0.35f,0f);
        player.Position = pos;
        Log.Debug($"RoomPos: {pos},Janitor pos: {player.Position}");
            
        player.CustomInfo = "Janitor";
        player.InfoArea |= PlayerInfoArea.Nickname;
        player.InfoArea &= ~PlayerInfoArea.Role;
    }
}