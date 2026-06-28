using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.RoundVictory.Core;

namespace Slafight_Plugin_EXILED.API.Features.RoundVictory.Sources;

/// <summary>
/// DanteBattle 中、ボス DANTE を討伐側 Chaos とは別の独立した勝利グループとして扱う定義。
/// SnowWarrior と同じく「カスタムロール所属」で 1 グループを構成するため、アクティブプロファイルに
/// 依存しない。ボス健在の間は「DANTE グループ + Insurgency グループ」の 2 グループが生存するので
/// ラウンドは終わらず、討伐側全滅で DANTE 勝利、ボス撃破（despawn）で Chaos 勝利に解決される。
/// </summary>
public sealed class DanteVictoryDefinitionSource : RoundVictoryDefinitionSource
{
    public override IEnumerable<RoundVictoryGroup> GetGroups()
    {
        yield return RoundVictoryRule.ForCustomRoles(
            CTeam.Others,
            priority: 20,
            customRoles: [CRoleTypeId.Dante],
            debugName: "DanteWin",
            specificReason: RoundEndReasons.DanteWin,
            requiresVanillaEndLock: true,
            isEnabled: context => context.ActiveEvent == SpecialEventType.DanteBattle).ToGroup();
    }
}
