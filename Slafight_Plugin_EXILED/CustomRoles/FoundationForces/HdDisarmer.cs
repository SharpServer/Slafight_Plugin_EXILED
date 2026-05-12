using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class HdDisarmer : CRole
{
    protected override string RoleName { get; set; } = "<color=#353535>ハンマーダウン 拘束兵</color>";
    protected override string Description { get; set; } = "当たると拘束できるスナイパーライフルを所持したNu-7の歩兵。\n敵の自由を奪い制圧を助ける。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.HdDisarmer;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "HdDisarmer";

    public override void SpawnRole(Player? player,RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.NtfPrivate);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 110;
        player.Health = player.MaxHealth;
        player.ClearInventory();
        player.GiveCItem<GunDisarmerRifle>();
        player.AddItem(ItemType.GunCrossvec);
        player.AddItem(ItemType.KeycardMTFOperative);
        player.AddItem(ItemType.Adrenaline);
        player.AddItem(ItemType.Medkit);
        player.AddItem(ItemType.Radio);
        CItem.Get<ArmorInfantry>()?.Give(player);
            
        player.SetAmmo(AmmoType.Nato9,120);
        player.SetAmmo(AmmoType.Nato556, 200);

        //PlayerExtensions.OverrideRoleName(player,$"{player.GroupName}","Hammer Down Infantry");
        player.CustomInfo = "<color=#727472>Hammer Down Disarmer</color>";
        player.InfoArea |= PlayerInfoArea.Nickname;
        player.InfoArea &= ~PlayerInfoArea.Role;
    }
}