using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class HdMarshal : CRole
{
    protected override string RoleName { get; set; } = "<color=#151515>ハンマーダウン 元帥</color>";
    protected override string Description { get; set; } = "Nu-7の師団を指揮し、勝利へと導く。\n敗北など許されない。突き進め！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.HdMarshal;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "HdMarshal";

    public override void SpawnRole(Player? player,RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.NtfCaptain);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 180;
        player.Health = player.MaxHealth;
        player.ClearInventory();
        Log.Debug("Giving Items to HdMarshal");
        player.AddItem(ItemType.KeycardMTFCaptain);
        CItem.Get<SerumC>()?.Give(player);
        CItem.Get<AdvancedMedkit>()?.Give(player);
        player.AddItem(ItemType.GrenadeHE);
        player.AddItem(ItemType.GrenadeHE);
        player.AddItem(ItemType.Radio);
        CItem.Get<ArmorVip>()?.Give(player);
        CItem.Get<GunN7Weltkrieg>()?.Give(player);
            
        player.SetAmmo(AmmoType.Nato556,250);

        //PlayerExtensions.OverrideRoleName(player,$"{player.GroupName}","Hammer Down Commander");
        player.CustomInfo = "<color=#727472>Hammer Down Marshal</color>";
        player.InfoArea |= PlayerInfoArea.Nickname;
        player.InfoArea &= ~PlayerInfoArea.Role;
    }
}