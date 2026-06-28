using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;

namespace Slafight_Plugin_EXILED.CustomRoles.Others;

/// <summary>
/// ボス「DANTE ─ 業火の指揮者」のカスタムロール。
/// 実体は <see cref="SpecialEvents.Events.DanteEvent"/> が NPC として完全制御する。
/// このロールは討伐側 Chaos とは別の独立した勝利グループにボスを所属させるための最小定義のみを持つ
/// （HP・装備・挙動は DanteEvent 側が管理し、ここでは付与しない）。
/// 勝利判定は <see cref="API.Features.RoundVictory.Sources.DanteVictoryDefinitionSource"/> が担当。
/// </summary>
public class DanteBoss : CRole
{
    protected override string RoleName { get; set; } = "<color=#ff1a1a>DANTE ─ 業火の指揮者</color>";
    protected override string Description { get; set; } = "地獄の業火を指揮する者。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Dante;
    protected override CTeam Team { get; set; } = CTeam.Others;
    protected override string UniqueRoleKey { get; set; } = "DanteBoss";

    // 素体は Foundation の NtfCaptain。討伐側 Chaos と vanilla 敵対関係になるので確実に被弾する。
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfCaptain;
}
