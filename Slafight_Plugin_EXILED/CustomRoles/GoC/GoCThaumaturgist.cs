using System.Collections.Generic;
using Exiled.API.Enums;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

namespace Slafight_Plugin_EXILED.CustomRoles.GoC;

public class GoCThaumaturgist : CRole
{
    protected override string RoleName { get; set; } = "GoC: Broken Dagger 超常技術スペシャリスト";
    protected override string Description { get; set; } = "SCP-148を用いて敵を制圧する\nPassive: VERITAS\n遠くにいる敵等を認識できる";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.GoCThaumaturgist;
    protected override CTeam Team { get; set; } = CTeam.GoC;
    protected override string UniqueRoleKey { get; set; } = "GoCThaumaturgist";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfSpecialist;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.GunE11SR,
        ItemType.KeycardMTFOperative,
        typeof(Scp148),
        ItemType.GrenadeHE,
        ItemType.Medkit,
        ItemType.SCP500,
        ItemType.Radio,
        typeof(ArmorInfantry),
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato556] = 140,
    };
    protected override string SpawnCustomInfo => "Global Occult Collision: Broken Dagger Thaumaturgist";
    protected override float SpawnEffectRefreshInterval => 3f;
    protected override IReadOnlyList<CRoleEffect> SpawnEffects =>
    [
        new(EffectType.Scp1344)
    ];
}
