using System.Collections.Generic;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Enums;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.SpawnSets.ChaosInsurgents;

/// <summary>
/// カオス・インサージェンシーの予備部隊 (ミニウェーブ) です。
/// </summary>
public sealed class ChaosBackupWaveSet : SpawnSet
{
    /// <inheritdoc />
    public override string Name => "Chaos Insurgency Backup";

    /// <inheritdoc />
    public override string Description => "カオス・インサージェンシーの予備部隊です。";

    /// <inheritdoc />
    public override Faction RespawnFaction => Faction.FoundationEnemy;

    /// <inheritdoc />
    public override bool IsMiniWave => true;

    public override int RespawnWeight => 100;

    /// <inheritdoc />
    public override float RespawnRatio => 1.0f;

    /// <inheritdoc />
    public override SpawnVehicle Vehicle => SpawnVehicle.ChaosVan;

    public override string Theme => "./WaveThemes/_w_chaos.ogg";

    protected override void BuildAnnouncement(SpawnAnnouncement a)
    {
        string noise = a.Seasonal("").On(SeasonTypeId.Halloween, "$pitch_.3 .g4 .g6 $pitch_1");

        a.Cassie(noise, $"Attention All personnel . Detected {a.SpawnCount} Chaos Insurgency Forces in Gate A . Please Terminate Them")
            .Subtitle($"全職員に通達。Gate Aに{a.SpawnCount}人の{SpawnAnnouncement.Colored("カオス・インサージェンシー", "#228b22")}部隊が検出されました。")
            .Line("見つけ次第終了してください。")
            .Note(SeasonTypeId.Christmas, "煙突からの侵入にも警戒してください。")
            .Note(SeasonTypeId.Halloween, "仮装による識別回避に警戒してください。");
    }

    public override IReadOnlyList<SpawnSetRoleDefinition> SpawnRoles =>
    [
        SpawnSetRoleDefinition.Vanilla(RoleTypeId.ChaosMarauder, count: 2, weight: 1.5f),
        SpawnSetRoleDefinition.Vanilla(RoleTypeId.ChaosRifleman, count: 99, weight: 4f),
    ];
}
