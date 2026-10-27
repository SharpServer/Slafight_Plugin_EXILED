using System.Collections.Generic;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;

namespace Slafight_Plugin_EXILED.API.Core.Samples;

/// <summary>
/// 役職の一括割り当ての書き方の見本です。
/// </summary>
public sealed class SampleSpawnSet : SpawnSet
{
    public override string Name => "Sample Spawn";

    public override string Description => "動作確認用の割り当てです。";

    /// <summary>
    /// 最大 4 人まで。-1 なら対象が居る限り配ります。
    /// </summary>
    public override int AllowedPlayerCount => 4;

    public override IReadOnlyList<SpawnSetRoleDefinition> SpawnRoles =>
    [
        // 必ず 1 人は出す。
        SpawnSetRoleDefinition.Custom<SampleRole>(count: 1, isForced: true),

        // 出やすさを半分にした枠。
        SpawnSetRoleDefinition.Custom<SampleRole>(count: 1, weight: 0.5f),

        // 残りはバニラ役職で埋める。
        SpawnSetRoleDefinition.Vanilla(RoleTypeId.Scientist, count: 99),
    ];
}
