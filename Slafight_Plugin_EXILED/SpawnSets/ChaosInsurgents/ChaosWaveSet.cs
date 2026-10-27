using System.Collections.Generic;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Enums;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.SpawnSets.ChaosInsurgents;

/// <summary>
/// カオス・インサージェンシーの通常波です。
/// </summary>
public sealed class ChaosWaveSet : SpawnSet
{
    public override string Name => "Chaos Insurgency";

    public override string Description => "カオス・インサージェンシーの通常波です。";

    public override Faction RespawnFaction => Faction.FoundationEnemy;

    public override int RespawnWeight => 100;

    public override float RespawnRatio => 1.0f;

    public override SpawnVehicle Vehicle => SpawnVehicle.ChaosVan;

    public override string Theme => "./WaveThemes/_w_chaos.ogg";

    protected override void BuildAnnouncement(SpawnAnnouncement a)
    {
        string noise = a.Seasonal("").On(SeasonTypeId.Halloween, "$pitch_.3 .g4 .g6 $pitch_1");

        a.Cassie(noise, $"Attention All personnel . Detected {a.SpawnCount} Chaos Insurgency Forces in Gate A . Please Terminate Them")
            .Subtitle($"全職員に通達。Gate Aに{a.SpawnCount}人の{SpawnAnnouncement.Colored("カオス・インサージェンシー", "#228b22")}部隊が検出されました。")
            .Line("見つけ次第終了してください。")
            .Note(SeasonTypeId.Christmas, "彼らはプレゼントを届けに来たのではありません。")
            .Note(SeasonTypeId.Halloween, "彼らの目的はお菓子ではありません。");
    }

    public override IReadOnlyList<SpawnSetRoleDefinition> SpawnRoles =>
    [
        SpawnSetRoleDefinition.Vanilla(RoleTypeId.ChaosRepressor, count: 2, weight: 1.5f),
        SpawnSetRoleDefinition.Vanilla(RoleTypeId.ChaosMarauder, count: 2, weight: 1.5f),
        SpawnSetRoleDefinition.Vanilla(RoleTypeId.ChaosRifleman, count: 99, weight: 4f),
    ];
}
