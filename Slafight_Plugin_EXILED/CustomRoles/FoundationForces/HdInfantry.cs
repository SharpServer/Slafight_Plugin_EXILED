using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class HdInfantry : CRole
{
    protected override string RoleName { get; set; } = "<color=#353535>ハンマーダウン 歩兵</color>";
    protected override string Description { get; set; } = "Nu-7の最下級兵だが、それでも強い装備が持たされている。\nNu-7とはこういう奴らなのだ";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.HdInfantry;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "HdInfantry";

    public override void SpawnRole(Player? player,RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.NtfPrivate);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 110;
        player.Health = player.MaxHealth;
        player.ClearInventory();
        Log.Debug("Giving Items to HdInfantry");
        player.AddItem(ItemType.GunCrossvec);
        player.AddItem(ItemType.KeycardMTFOperative);
        player.AddItem(ItemType.Adrenaline);
        player.AddItem(ItemType.Medkit);
        player.AddItem(ItemType.GrenadeFlash);
        player.AddItem(ItemType.GrenadeHE);
        player.AddItem(ItemType.Radio);
        CItem.Get<ArmorInfantry>()?.Give(player);
            
        player.AddAmmo(AmmoType.Nato9,140);

        //PlayerExtensions.OverrideRoleName(player,$"{player.GroupName}","Hammer Down Infantry");
        player.CustomInfo = "<color=#727472>Hammer Down Infantry</color>";
        player.InfoArea |= PlayerInfoArea.Nickname;
        player.InfoArea &= ~PlayerInfoArea.Role;
    }
}