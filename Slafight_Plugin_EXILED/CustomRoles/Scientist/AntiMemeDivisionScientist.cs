using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.Scientist;

public class AntiMemeDivisionScientist : CRole
{
    protected override string RoleName { get; set; } = "反ミーム部門職員";
    protected override string Description { get; set; } = "現在貴方の部門は壊滅状態に陥っている...\n" +
                                                          "下層のDクラス収容房最奥にある反ミーム爆弾を起動してこのアウトブレイクをリセットしなければならない。\n" +
                                                          "<color=red>例え命を落とそうとも</color>";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.AntiMemeDivisionScientist;
    protected override CTeam Team { get; set; } = CTeam.Scientists;
    protected override string UniqueRoleKey { get; set; } = "AMDScientist";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Scientist;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.GunCOM15,
        typeof(KeycardHeadResearcherGeneric),
        typeof(ClassZMemoryForcePil)
    ];
    protected override Vector3? SpawnPosition => Door.Get(DoorType.Intercom).Position + Vector3.up * 1.25f;
    protected override string SpawnCustomInfo => "Anti Memetic Division Scientist";
}
