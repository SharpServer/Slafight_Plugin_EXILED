using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.Guards;

public class ChamberGuard : CRole
{
    protected override string RoleName { get; set; } = "収容室警備";
    protected override string Description { get; set; } = "Dクラス職員やオブジェクトの異常を監視する。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.ChamberGuard;
    protected override CTeam Team { get; set; } = CTeam.Guards;
    protected override string UniqueRoleKey { get; set; } = "ChamberGuard";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.FacilityGuard;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.GunFSP9,
        ItemType.KeycardGuard,
        ItemType.Medkit,
        ItemType.ArmorLight,
        ItemType.Radio,
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato9] = 100,
    };
    protected override Vector3? SpawnPosition => Door.Get(DoorType.Scp173Connector).Position + new Vector3(0f, 0.35f, 0f);
    protected override string SpawnCustomInfo => "Chamber Guard";

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        Log.Debug($"RoomPos: {player.Position},CGuard pos: {player.Position}");
    }
}
