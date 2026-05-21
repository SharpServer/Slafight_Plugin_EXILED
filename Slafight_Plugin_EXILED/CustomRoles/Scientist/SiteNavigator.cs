using System.Collections.Generic;
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
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Scientist;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        typeof(SNAV300),
        typeof(KeycardSiteNavigator),
        ItemType.Medkit,
        ItemType.Flashlight,
    ];
    protected override string SpawnCustomInfo => "Site Navigator";
}
