using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.CustomRoles.Scientist;

public class SiteNavigator : CRole
{
    protected override string RoleName { get; set; } = "サイトナビゲーター";
    protected override string Description { get; set; } = "携帯用マップ端末\"S-NAV\"を持った研究員。\n" +
                                                          "つねに構造が変化し続けるサイト-02において、S-NAVは必需品である";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.SiteNavigator;
    protected override CTeam Team { get; set; } = CTeam.Scientists;
    protected override string UniqueRoleKey { get; set; } = "SiteNavigator";

    public override void SpawnRole(Player? player, RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        if (player == null) return;

        player.Role.Set(RoleTypeId.Scientist, roleSpawnFlags);
        base.SpawnRole(player, roleSpawnFlags);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 100;
        player.Health = player.MaxHealth;

        player.ClearInventory();
        CItem.Get<SNAV300>()?.Give(player);
        CItem.Get<KeycardSiteNavigator>()?.Give(player);
        player.AddItem(ItemType.Medkit);
        player.AddItem(ItemType.Flashlight);

        player.SetCustomInfo("Site Navigator");
    }
}
