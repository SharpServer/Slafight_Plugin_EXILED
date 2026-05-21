using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.Guards;

public class SecurityChief : CRole
{
    protected override string RoleName { get; set; } = "警備主任";
    protected override string Description { get; set; } = "施設内の職員を外に脱出させよう！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.SecurityChief;
    protected override CTeam Team { get; set; } = CTeam.Guards;
    protected override string UniqueRoleKey { get; set; } = "SecurityChief";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.FacilityGuard;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.Medkit,
        ItemType.Medkit,
        ItemType.ArmorCombat,
        ItemType.Radio,
        typeof(KeycardSecurityChief),
        typeof(GunFSP18),
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato9] = 130,
    };
    protected override string SpawnCustomInfo => "Security Chief";

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        Timing.CallDelayed(0.05f, () =>
        {
            player.Position = Room.Get(RoomType.EzChef).WorldPosition(Vector3.up * 0.75f);
        });
    }
}
