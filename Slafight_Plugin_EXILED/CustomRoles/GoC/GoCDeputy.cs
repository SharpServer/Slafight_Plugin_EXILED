using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

namespace Slafight_Plugin_EXILED.CustomRoles.GoC;

public class GoCDeputy : CRole
{
    protected override string RoleName { get; set; } = "GoC: Broken Dagger 副官";
    protected override string Description { get; set; } = "部隊の任務遂行を補助する\nPassive: VERITAS\n遠くにいる敵等を認識できる";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.GoCDeputy;
    protected override CTeam Team { get; set; } = CTeam.GoC;
    protected override string UniqueRoleKey { get; set; } = "GoCDeputy";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfSergeant;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.GunCom45,
        ItemType.GunE11SR,
        ItemType.KeycardMTFOperative,
        ItemType.Medkit,
        ItemType.GrenadeHE,
        ItemType.Radio,
        typeof(FlashBangE),
        typeof(ArmorInfantry),
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato556] = 140,
    };
    protected override string SpawnCustomInfo => "Global Occult Collision: Broken Dagger Deputy";
    protected override float SpawnEffectRefreshInterval => 3f;
    protected override IReadOnlyList<CRoleEffect> SpawnEffects =>
    [
        new(EffectType.Scp1344)
    ];
}
