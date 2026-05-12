using Exiled.API.Enums;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.Guards;

public class SecurityChief : CRole
{
    protected override string RoleName { get; set; } = "警備主任";
    protected override string Description { get; set; } = "施設内の職員を外に脱出させよう！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.SecurityChief;
    protected override CTeam Team { get; set; } = CTeam.Guards;
    protected override string UniqueRoleKey { get; set; } = "SecurityChief";

    public override void SpawnRole(Player? player,RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.FacilityGuard);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 100;
        player.Health = player.MaxHealth;
        player.ClearInventory();
        Log.Debug("Giving Items to SecurityChief");
        player.AddItem(ItemType.Medkit);
        player.AddItem(ItemType.Medkit);
        player.AddItem(ItemType.ArmorCombat);
        player.AddItem(ItemType.Radio);
        CItem.Get<KeycardSecurityChief>()?.Give(player);
        CItem.Get<GunFSP18>()?.Give(player);
        player.AddAmmo(AmmoType.Nato9,130);
            
        player.SetCustomInfo("Security Chief");
        Timing.CallDelayed(0.05f, () =>
        {
            player.Position = Room.Get(RoomType.EzChef).WorldPosition(Vector3.up * 0.75f);
        });
    }
}