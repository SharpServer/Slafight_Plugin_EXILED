using System.Collections.Generic;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.CustomRoles.Scientist;

public class FacilityManager : CRole
{
    protected override string RoleName { get; set; } = "<color=#dc143c>施設管理官</color>";
    protected override string Description { get; set; } = "施設を管理・運営する重要な科学者。\n" +
                                                          "区画管理官や警備員たちに指示を出し、収容違反に対処する。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.FacilityManager;
    protected override CTeam Team { get; set; } = CTeam.Scientists;
    protected override string UniqueRoleKey { get; set; } = "FacilityManager";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Scientist;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.GunCrossvec,
        ItemType.KeycardFacilityManager,
        ItemType.Medkit,
        ItemType.Medkit,
        ItemType.ArmorCombat,
        ItemType.Radio,
    ];
    protected override string SpawnCustomInfo => "Facility Manager";

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        Timing.CallDelayed(0.05f, () =>
        {
            player.Position = MapFlags.FacilityManagerSpawnPoint;
        });
    }
}
