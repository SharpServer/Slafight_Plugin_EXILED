using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class SnePurify : CRole
{
    protected override string RoleName { get; set; } = "<color=#FF1493>シー・ノー・イービル 修正兵</color>";
    protected override string Description { get; set; } = "気狂いどもを正常しろ！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.SnePurify;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "SnePurify";

    public override void SpawnRole(Player? player,RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.NtfPrivate);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 125;
        player.Health = player.MaxHealth;
        player.ClearInventory();
        player.AddItem(ItemType.SCP1509);
        player.AddItem(ItemType.KeycardMTFOperative);
        player.AddItem(ItemType.Adrenaline);
        player.AddItem(ItemType.Medkit);
        player.AddItem(ItemType.GrenadeFlash);
        player.AddItem(ItemType.GrenadeHE);
        player.AddItem(ItemType.Radio);
        player.AddItem(ItemType.ArmorCombat);
            
        player.AddAmmo(AmmoType.Nato9,140);

        //PlayerExtensions.OverrideRoleName(player,$"{player.GroupName}","Hammer Down Infantry");
        player.SetCustomInfo("<color=#FF1493>See No Evil Purify</color>");
    }
}