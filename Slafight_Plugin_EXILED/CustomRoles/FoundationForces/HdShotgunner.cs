using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class HdShotgunner : CRole
{
    protected override string RoleName { get; set; } = "<color=#353535>ハンマーダウン 砲弾兵</color>";
    protected override string Description { get; set; } = "ショットガンを二丁持ちしたNu-7の歩兵。\n素早い猛攻で敵を粉砕する。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.HdShotgunner;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "HdShotgunner";

    public override void SpawnRole(Player? player,RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.NtfPrivate);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 110;
        player.Health = player.MaxHealth;
        player.ClearInventory();
        player.AddItem(ItemType.GunShotgun);
        player.AddItem(ItemType.GunShotgun);
        player.AddItem(ItemType.KeycardMTFOperative);
        player.AddItem(ItemType.Adrenaline);
        player.AddItem(ItemType.Medkit);
        player.AddItem(ItemType.Radio);
        CItem.Get<ArmorInfantry>()?.Give(player);
            
        player.SetAmmo(AmmoType.Ammo12Gauge,200);

        //PlayerExtensions.OverrideRoleName(player,$"{player.GroupName}","Hammer Down Infantry");
        CustomInfoDisplay.Apply(player, "<color=#727472>Hammer Down Shotgunner</color>");
    }
}
