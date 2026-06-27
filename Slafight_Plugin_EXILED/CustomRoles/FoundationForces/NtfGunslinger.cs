using System.Collections.Generic;
using Exiled.API.Enums;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class NtfGunslinger : CRole
{
    protected override string RoleName { get; set; } = "九尾狐 銃撃兵";
    protected override string Description { get; set; } = 
        "<size=23>NTFの中でも特にアサルトライフルの扱いに長け、新兵器のテスターとして抜擢されたライフルマン。\n" +
        "アサルトライフルと弾倉式グレネードランチャーが一体化した\n" +
        "マルチウェポン\"XE-11K MR\"を扱い、戦場でのあらゆる状況に対応する</size>";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.NtfGunslinger;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "NtfGunslinger";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfSergeant;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        typeof(GunXE11KMR),
        ItemType.KeycardMTFOperative,
        typeof(ArmorInfantry),
        ItemType.Medkit,
        ItemType.Radio,
        ItemType.Flashlight,
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato556] = 120,
    };
    protected override string SpawnCustomInfo => "Nine-tailed Fox Gunslinger";

}
