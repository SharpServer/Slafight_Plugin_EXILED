using System.Collections.Generic;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.CustomRoles.Fifthists;
using Slafight_Plugin_EXILED.CustomTeams;

namespace Slafight_Plugin_EXILED.SpawnSets.Fifthists;

/// <summary>
/// 第五教会の通常波です。SCP-3005 か司祭が場に居るときだけ出ます。
/// </summary>
public sealed class FifthistsWaveSet : SpawnSet
{
    public override string Name => "Fifthists";

    public override string Description => "第五教会の通常波です。";

    public override Faction RespawnFaction => Faction.FoundationEnemy;

    /// <summary>祝福中はカオスの 100 に対して約 60:40。</summary>
    public override int RespawnWeight => 67;

    public override float RespawnRatio => 1.0f;

    public override string Theme => "./WaveThemes/_w_fifthists.ogg";

    protected override bool TriggerCondition() => FifthistTeam.IsBlessed;

    protected override void BuildAnnouncement(SpawnAnnouncement a) => FifthistsAnnouncement.Build(a);

    public override IReadOnlyList<SpawnSetRoleDefinition> SpawnRoles =>
    [
        SpawnSetRoleDefinition.Custom<FifthistPriest>(count: 1, isForced: true),
        SpawnSetRoleDefinition.Custom<FifthistRescuer>(count: 3),
        SpawnSetRoleDefinition.Custom<FifthistGuidance>(count: 1),
        SpawnSetRoleDefinition.Custom<FifthistMindblaster>(count: 1),
        SpawnSetRoleDefinition.Custom<FifthistConvert>(count: 99),
    ];
}
