using System.Collections.Generic;
using Exiled.API.Enums;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.CustomMaps;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class PdxWarden : CRole
{
    protected override string RoleName { get; set; } = $"<color={ServerColors.Carmine}><b>Pandra's Box: Warden</b></color>";
    protected override string Description { get; set; } = $"Omega-7の監督官。\n<b>アベルを監視し、暴走時には起爆スイッチを押して制御する事。</b>";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.PdxWarden;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "PdxWarden";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfCaptain;
    protected override float? SpawnMaxHealth => 130f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.KeycardMTFCaptain,
        ItemType.GrenadeHE,
        ItemType.GrenadeFlash,
        ItemType.Adrenaline,
        ItemType.Medkit,
        typeof(PandraBreaker),
        typeof(ArmorVip),
        typeof(GunFRMGX),
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato556] = 260,
    };
    protected override string SpawnCustomInfo => $"<color={ServerColors.Carmine}>Pandra's Box Warden</color>";
}
