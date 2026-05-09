using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class SneOperator : CRole
{
    protected override string RoleName { get; set; } = "<color=#FF1493>シー・ノー・イービル オペレーター</color>";
    protected override string Description { get; set; } = "部隊を指揮し、気狂いどもに正常性による一撃を与えよ！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.SneOperator;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "SneOperator";

    public override void SpawnRole(Player? player,RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.NtfCaptain);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 150;
        player.Health = player.MaxHealth;
        player.ClearInventory();
        player.AddItem(ItemType.GunFRMG0);
        player.AddItem(ItemType.KeycardMTFCaptain);
        CItem.Get<SerumC>()?.Give(player);
        CItem.Get<AntiMemeGoggle>()?.Give(player);
        CItem.Get<NeutralizeGrenade>()?.Give(player);
        CItem.Get<NeutralizeGrenade>()?.Give(player);
        player.AddItem(ItemType.Radio);
        player.AddItem(ItemType.ArmorHeavy);
            
        player.AddAmmo(AmmoType.Nato556,220);

        //PlayerExtensions.OverrideRoleName(player,$"{player.GroupName}","Hammer Down Infantry");
        player.SetCustomInfo("<color=#FF1493>See No Evil Operator</color>");
    }
}