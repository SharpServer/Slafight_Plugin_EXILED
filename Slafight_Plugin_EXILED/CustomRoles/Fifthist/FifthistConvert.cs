using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.Fifthist;

public class FifthistConvert : CRole
{
    protected override string RoleName { get; set; } = "<color=#ff5ffa>第五教会 改宗者</color>";
    protected override string Description { get; set; } = "貴方は新たに第五教会に加わった。全てを第五に捧げるのです。\nSCP-1425を使って、更に第五を広めろ！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.FifthistConvert;
    protected override CTeam Team { get; set; } = CTeam.Fifthists;
    protected override string UniqueRoleKey { get; set; } = "FifthistConvert";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Tutorial;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.GunA7,
        ItemType.Medkit,
        ItemType.ArmorCombat,
        typeof(KeycardFifthist),
        typeof(Scp1425),
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato762] = 170,
    };
    protected override Vector3? SpawnPosition => new Vector3(124f, 289f, 21f);
    protected override string SpawnCustomInfo => "<color=#FF0090>Fifthist Convert</color>";
}
