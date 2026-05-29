using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

namespace Slafight_Plugin_EXILED.CustomRoles.GoC;

public class GoCCommunications : CRole
{
    protected override string RoleName { get; set; } = "GoC: Broken Dagger 通信スペシャリスト";
    protected override string Description { get; set; } = "SNAVを用いて探索を行う\nPassive: VERITAS\n遠くにいる敵等を認識できる";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.GoCCommunications;
    protected override CTeam Team { get; set; } = CTeam.GoC;
    protected override string UniqueRoleKey { get; set; } = "GoCCommunications";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfSpecialist;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.GunE11SR,
        ItemType.ParticleDisruptor,
        ItemType.KeycardMTFOperative,
        typeof(SNAVUltimate),
        ItemType.Medkit,
        typeof(SerumC),
        ItemType.Radio,
        typeof(ArmorInfantry),
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato556] = 140,
    };
    protected override string SpawnCustomInfo => "Global Occult Collision: Broken Dagger Communications";
    protected override float SpawnEffectRefreshInterval => 3f;
    protected override IReadOnlyList<CRoleEffect> SpawnEffects =>
    [
        new(EffectType.Scp1344)
    ];
}
