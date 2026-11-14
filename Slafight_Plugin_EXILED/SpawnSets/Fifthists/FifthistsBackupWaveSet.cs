using System.Collections.Generic;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.CustomRoles.Fifthists;
using Slafight_Plugin_EXILED.CustomTeams;

namespace Slafight_Plugin_EXILED.SpawnSets.Fifthists;

/// <summary>
/// 第五教会の予備部隊 (ミニウェーブ) です。SCP-3005 か司祭が場に居るときだけ出ます。
/// </summary>
public sealed class FifthistsBackupWaveSet : SpawnSet
{
    public override string Name => "Fifthists Backup";

    public override string Description => "第五教会の予備部隊です。";

    public override Faction RespawnFaction => Faction.FoundationEnemy;

    public override bool IsMiniWave => true;

    /// <summary>祝福中はカオスの 100 に対して約 60:40。</summary>
    public override int RespawnWeight => 67;

    public override float RespawnRatio => 0.5f;

    public override string Theme => "./WaveThemes/_w_fifthists.ogg";

    protected override bool TriggerCondition() => FifthistTeam.IsBlessed;

    protected override void BuildAnnouncement(SpawnAnnouncement a) => FifthistsAnnouncement.Build(a);

    public override IReadOnlyList<SpawnSetRoleDefinition> SpawnRoles =>
    [
        SpawnSetRoleDefinition.Custom<FifthistRescuer>(count: 1),
        SpawnSetRoleDefinition.Custom<FifthistGuidance>(count: 1),
        SpawnSetRoleDefinition.Custom<FifthistConvert>(count: 99),
    ];
}
