using System.Collections.Generic;
using Exiled.API.Enums;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.CustomMaps.Core;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class NtfCadet : CRole
{
    protected override string RoleName { get; set; } = "九尾狐 士官候補生";
    protected override string Description { get; set; } = "MTFの士官養成課程を受講し訓練に励んでいる士官候補生。\n" +
                                                          "本来まだ訓練中だがサイト-02の死守の為に保安部隊の隊長として緊急動員された。\n" +
                                                          "その為装備が一世代前の型落ち品となっている。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.NtfCadet;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "NtfCadet";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfPrivate;
    protected override Vector3? SpawnPosition => MapFlags.FirstTeamSpawnPoint;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        typeof(GunProject90),
        ItemType.KeycardMTFPrivate,
        ItemType.ArmorCombat,
        ItemType.Medkit,
        ItemType.Radio,
        ItemType.Flashlight
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato9] = 120,
    };
    protected override string SpawnCustomInfo => "Nine-tailed Fox Cadet";

}
