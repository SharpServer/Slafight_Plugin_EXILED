using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class HdCommander : CRole
{
    protected override string RoleName { get; set; } = "<color=#252525>ハンマーダウン 指揮官</color>";
    protected override string Description { get; set; } = "Nu-7の歩兵たちを指揮し、制圧を進める。\n偉大なる我らが元帥の指示に従え！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.HdCommander;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "HdCommander";

    public override void SpawnRole(Player? player,RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.NtfSergeant);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 125;
        player.Health = player.MaxHealth;
        player.ClearInventory();
        Log.Debug("Giving Items to HdCommander");
        player.AddItem(ItemType.KeycardMTFOperative);
        player.AddItem(ItemType.Adrenaline);
        player.AddItem(ItemType.Medkit);
        player.AddItem(ItemType.GrenadeHE);
        player.AddItem(ItemType.GrenadeHE);
        player.AddItem(ItemType.Radio);
        CItem.Get<ArmorVip>()?.Give(player);
        CItem.Get<GunN7CR>()?.Give(player);
            
        player.AddAmmo(AmmoType.Nato556,200);

        //PlayerExtensions.OverrideRoleName(player,$"{player.GroupName}","Hammer Down Commander");
        player.CustomInfo = "<color=#727472>Hammer Down Commander</color>";
        player.InfoArea |= PlayerInfoArea.Nickname;
        player.InfoArea &= ~PlayerInfoArea.Role;
    }
}