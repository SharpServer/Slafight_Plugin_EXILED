using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.Scientist;

public class Engineer : CRole
{
    protected override string RoleName { get; set; } = "エンジニア";
    protected override string Description { get; set; } = "施設内の様々なシステム等を整備する職員。\n" +
                                                          "Toolboxを用いてSCPを食い止めたり、他職員の脱出等をサポートせよ。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Engineer;
    protected override CTeam Team { get; set; } = CTeam.Scientists;
    protected override string UniqueRoleKey { get; set; } = "Engineer";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Scientist;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.KeycardContainmentEngineer,
        ItemType.Medkit,
        ItemType.Medkit,
        typeof(Toolbox),
    ];
    protected override string SpawnCustomInfo => "Engineer";

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        var room = Room.Get(RoomType.HczTestRoom);
        if (room != null)
            player.Position = room.WorldPosition(new Vector3(0f, 1f, 0f));
    }
}
