using System.Collections.Generic;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Enums;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;

namespace Slafight_Plugin_EXILED.API.Core.Samples;

/// <summary>
/// リスポーンウェーブの書き方の見本です。
///
/// <b>これは <see cref="SpawnSet"/> の派生クラスそのものです。</b>
/// 波専用の基底クラスも、波を指す enum も経由していません。
/// 陣営・重み・割合・アナウンスは、この波が自分で名乗ります。
/// </summary>
/// <remarks>
/// <see cref="SpawnSet.RespawnWeight"/> が 0 より大きい <see cref="SpawnSet"/> が抽選対象の波になります。
/// この見本は本番の抽選に混ざらないよう 0 にしてあるので、実際には出ません。
/// 試すときは <c>slc wave SampleWave</c> で重みに関係なく出せます。
/// </remarks>
public sealed class SampleWave : SpawnSet
{
    public override string Name => "Sample Wave";

    public override string Description => "動作確認用のリスポーンウェーブです。";

    public override Faction RespawnFaction => Faction.FoundationStaff;

    public override int RespawnWeight => 0;

    // 車両なしなら override 不要 (SpawnVehicle.None)。カオス車両は ChaosVan を指定します。
    // バニラが先に出す演出は SpawnSystem が抑えるので、OnSpawning から召喚する必要はありません。
    public override SpawnVehicle Vehicle => SpawnVehicle.NtfChopper;

    /// <summary>
    /// 待機者の 6 割だけ出します。0 人になることはありません。
    /// </summary>
    public override float RespawnRatio => 0.6f;

    public override (string Cassie, string Subtitle) Announcement(int spawnCount, string unitName) =>
        ("MtfUnit Epsilon 11 Designated Sample HasEntered AllRemaining",
         $"<color=#00b7eb>見本部隊</color>が施設に到着しました。({spawnCount}名)");

    public override IReadOnlyList<SpawnSetRoleDefinition> SpawnRoles =>
    [
        SpawnSetRoleDefinition.Custom<SampleRole>(count: 1, isForced: true),
        SpawnSetRoleDefinition.Vanilla(RoleTypeId.NtfPrivate, count: 99),
    ];
}
