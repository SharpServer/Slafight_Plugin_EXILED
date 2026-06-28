using System.Collections.Generic;
using Exiled.API.Enums;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

namespace Slafight_Plugin_EXILED.CustomRoles.ChaosInsurgency;

/// <summary>
/// 「ダンテ討伐部隊」── クソ強い DANTE と互角に渡り合うための専用レイドロール。
/// チームは <see cref="CTeam.ChaosInsurgency"/> 固定（既定プロファイルで Insurgency 勝利グループに入り、
/// DANTE グループと 2 勢力共存になる）。<see cref="SpecialEvents.Events.DanteEvent"/> が討伐側を
/// この役職にし、部隊 Wave で増援する。
///
/// 対ボス主力は <see cref="GunM82"/>（GunE11SR ベースの通常ダメージ＝仮想 HP に素直に通る）。
/// ParticleDisruptor 系（GunGoCRailgun）は被弾フックを迂回し得るので採用しない。
/// </summary>
public class DanteSlayer : CRole
{
    protected override string RoleName { get; set; } = "<color=#39ff14>ダンテ討伐部隊</color>";
    protected override string Description { get; set; } =
        "緑の巨塊 DANTE を討つために編成された対異常存在制圧部隊。対物ライフルと制圧火器で粘体を削り切れ。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.DanteSlayer;
    protected override CTeam Team { get; set; } = CTeam.ChaosInsurgency;
    protected override string UniqueRoleKey { get; set; } = "DanteSlayer";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.ChaosRifleman;
    protected override float? SpawnMaxHealth => 6500f;

    protected override IReadOnlyList<object> SpawnItems =>
    [
        typeof(GunM82),          // 対物ライフル（対ボス主力／通常ダメージ）
        typeof(GunSuperLogicer), // 制圧・粘体処理
        typeof(ArmorVip),
        typeof(AdvancedMedkit),
        ItemType.Medkit,
        ItemType.SCP500,
        ItemType.SCP500,
        ItemType.Adrenaline,
        ItemType.GrenadeHE,      // 分裂粘塊の処理用
        ItemType.GrenadeHE,
    ];

    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato556] = 1500, // M82 (GunE11SR) ── AmmoDrain が大きいので多めに
        [AmmoType.Nato762] = 1200, // SuperLogicer ── 制圧で減りが速い
    };

    protected override string SpawnCustomInfo => "DANTE SLAYER";
}
