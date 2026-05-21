using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class SnePurify : CRole
{
    protected override string RoleName { get; set; } = "<color=#FF1493>シー・ノー・イービル 修正兵</color>";
    protected override string Description { get; set; } = "気狂いどもを正常しろ！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.SnePurify;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "SnePurify";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfPrivate;
    protected override float? SpawnMaxHealth => 125f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.SCP1509,
        ItemType.KeycardMTFOperative,
        ItemType.Adrenaline,
        ItemType.Medkit,
        ItemType.GrenadeFlash,
        ItemType.GrenadeHE,
        ItemType.Radio,
        ItemType.ArmorCombat,
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato9] = 120,
    };
    protected override string SpawnCustomInfo => "<color=#FF1493>See No Evil Purify</color>";
}
