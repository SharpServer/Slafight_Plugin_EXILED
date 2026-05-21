using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.CustomRoles.ChaosInsurgency;

public class ChaosIntruder : CRole
{
    protected override string RoleName { get; set; } = "カオス・インサージェンシー 侵入兵";
    protected override string Description { get; set; } = "施設に侵入した小規模部隊。警備隊の壊滅及び仲間の脱出を目指せ！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.ChaosIntruder;
    protected override CTeam Team { get; set; } = CTeam.ChaosInsurgency;
    protected override string UniqueRoleKey { get; set; } = "ChaosIntruder";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.ChaosMarauder;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.GrenadeFlash,
        ItemType.Medkit,
        ItemType.Adrenaline,
        ItemType.ArmorCombat,
        typeof(GunSuppressiver),
        typeof(KeycardChaosIntruder),
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato9] = 100,
    };
    protected override UnityEngine.Vector3? SpawnPosition => MapFlags.FirstTeamSpawnPoint;
    protected override string SpawnCustomInfo => "Chaos Insurgency Intruder";

}
