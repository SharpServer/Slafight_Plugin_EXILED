using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class SneNeutralitist : CRole
{
    protected override string RoleName { get; set; } = "<color=#FF1493>シー・ノー・イービル 破力兵</color>";
    protected override string Description { get; set; } = "気狂いどもを食い止めろ！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.SneNeutralitist;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "SneNeutralitist";

    public override void SpawnRole(Player? player,RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.NtfPrivate);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 125;
        player.Health = player.MaxHealth;
        player.ClearInventory();
        player.AddItem(ItemType.GunE11SR);
        player.AddItem(ItemType.KeycardMTFOperative);
        player.AddItem(ItemType.Adrenaline);
        player.AddItem(ItemType.Medkit);
        CItem.Get<NeutralizeGrenade>()?.Give(player);
        CItem.Get<NeutralizeGrenade>()?.Give(player);
        player.AddItem(ItemType.Radio);
        player.AddItem(ItemType.ArmorCombat);
            
        player.SetAmmo(AmmoType.Nato556,140);

        //PlayerExtensions.OverrideRoleName(player,$"{player.GroupName}","Hammer Down Infantry");
        player.SetCustomInfo("<color=#FF1493>See No Evil Neutralitist</color>");
    }
}