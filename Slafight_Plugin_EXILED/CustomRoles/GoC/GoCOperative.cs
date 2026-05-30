using System.Collections.Generic;
using Exiled.API.Enums;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

namespace Slafight_Plugin_EXILED.CustomRoles.GoC;

public class GoCOperative : CRole
{
    protected override string RoleName { get; set; } = "GoC: Broken Dagger 工作員";
    protected override string Description { get; set; } = "部隊の任務を遂行する\nPassive: VERITAS\n遠くにいる敵等を認識できる";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.GoCOperative;
    protected override CTeam Team { get; set; } = CTeam.GoC;
    protected override string UniqueRoleKey { get; set; } = "GoCOperative";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfPrivate;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.GunCrossvec,
        ItemType.GunShotgun,
        ItemType.KeycardMTFOperative,
        ItemType.Medkit,
        ItemType.Radio,
        ItemType.ArmorCombat,
        typeof(GoCRecruitPaper),
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato9] = 140,
    };
    protected override string SpawnCustomInfo => "Global Occult Collision: Broken Dagger Operative";
    protected override float SpawnEffectRefreshInterval => 3f;
    protected override IReadOnlyList<CRoleEffect> SpawnEffects =>
    [
        new(EffectType.Scp1344)
    ];
}
