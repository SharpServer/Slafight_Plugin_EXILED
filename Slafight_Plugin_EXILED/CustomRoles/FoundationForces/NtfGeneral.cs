using System.Collections.Generic;
using Exiled.API.Enums;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class NtfGeneral : CRole
{
    protected override string RoleName { get; set; } = "<color=blue>九尾狐 司令官</color>";
    protected override string Description { get; set; } = "Epsilon-11を率いる高位の司令官。\n隊長等と連携し、確実に施設に安定をもたらせ！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.NtfGeneral;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "NtfGeneral";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfCaptain;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.KeycardMTFCaptain,
        ItemType.GrenadeHE,
        ItemType.GrenadeHE,
        ItemType.Radio,
        typeof(SerumD),
        typeof(AdvancedMedkit),
        typeof(ArmorVip),
        typeof(GunFRMGX),
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato556] = 320,
    };
    protected override string SpawnCustomInfo => "Nine-tailed Fox General";

}
