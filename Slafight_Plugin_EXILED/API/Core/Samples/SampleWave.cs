using System.Collections.Generic;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Enums;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.API.Core.Samples;

/// <summary>
/// リスポーンウェーブの書き方の見本です。重み 0 なので抽選には出ません (<c>slc wave SampleWave</c> で手動実行)。
/// </summary>
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

    /// <summary>
    /// 出撃放送です。残存 SCP 数は自動で後ろに付きます。
    /// </summary>
    protected override void BuildAnnouncement(SpawnAnnouncement a)
    {
        // 季節で変えたい語だけ Seasonal(...).On(...) で差し替える。
        string unitName = a.Seasonal("見本部隊").On(SeasonTypeId.Halloween, "お化け見本部隊");

        a.Cassie("MtfUnit Epsilon 11 Designated Sample", a.UnitCassie, "HasEntered AllRemaining")
            .Subtitle($"{SpawnAnnouncement.Colored(unitName + a.UnitSuffix, "#00b7eb")}が施設に到着しました。({a.SpawnCount}名)")
            .Note(SeasonTypeId.Christmas, "見本部隊もクリスマス仕様です。");
    }

    public override IReadOnlyList<SpawnSetRoleDefinition> SpawnRoles =>
    [
        SpawnSetRoleDefinition.Custom<SampleRole>(count: 1, isForced: true),
        SpawnSetRoleDefinition.Vanilla(RoleTypeId.NtfPrivate, count: 99),
    ];
}
