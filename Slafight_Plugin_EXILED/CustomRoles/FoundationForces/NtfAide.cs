using System.Collections.Generic;
using Exiled.API.Enums;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class NtfAide : CRole
{
    protected override string RoleName { get; set; } = "九尾狐 副官";
    protected override string Description { get; set; } = "隊長の補佐を目的とし、万一の際は代理・臨時隊長として指示を下せる。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.NtfLieutenant;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "NtfAide";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfSergeant;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.GunE11SR,
        ItemType.KeycardMTFCaptain,
        ItemType.Adrenaline,
        ItemType.Adrenaline,
        ItemType.Medkit,
        ItemType.GrenadeFlash,
        ItemType.ArmorHeavy,
        ItemType.Radio,
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato9] = 130,
    };
    protected override string SpawnCustomInfo => "Nine-tailed Fox Lieutenant";

}
