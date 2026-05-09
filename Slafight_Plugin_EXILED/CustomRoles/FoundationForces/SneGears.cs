using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class SneGears : CRole
{
    protected override string RoleName { get; set; } = "<color=#FF1493>シー・ノー・イービル 対圧兵</color>";
    protected override string Description { get; set; } = "気狂いどもに一撃を与えろ！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.SneGears;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "SneGears";

    public override void SpawnRole(Player? player,RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.NtfSergeant);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 125;
        player.Health = player.MaxHealth;
        player.ClearInventory();
        player.AddItem(ItemType.GunE11SR);
        player.AddItem(ItemType.KeycardMTFOperative);
        CItem.Get<SerumC>()?.Give(player);
        player.AddItem(ItemType.Medkit);
        CItem.Get<AntiMemeGoggle>()?.Give(player);
        player.AddItem(ItemType.Radio);
        player.AddItem(ItemType.ArmorHeavy);
            
        player.AddAmmo(AmmoType.Nato556,140);

        //PlayerExtensions.OverrideRoleName(player,$"{player.GroupName}","Hammer Down Infantry");
        player.SetCustomInfo("<color=#FF1493>See No Evil Gears</color>");
    }
}