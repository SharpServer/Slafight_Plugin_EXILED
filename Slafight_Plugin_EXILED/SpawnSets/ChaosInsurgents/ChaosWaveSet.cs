using System.Collections.Generic;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.CustomMaps;

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

    public override string Theme => "./WaveThemes/_w_chaos.ogg";

    public override (string Cassie, string Subtitle) Announcement(int spawnCount, string unitName) =>
        ($"Attention All personnel . Detected {spawnCount} Chaos Insurgency Forces in Gate A . Please Terminate Them",
         $"全職員に通達。Gate Aに{spawnCount}人の<color=#228b22>カオス・インサージェンシー</color>部隊が検出されました。" +
         "<split>見つけ次第終了してください。");

    public override IReadOnlyList<SpawnSetRoleDefinition> SpawnRoles =>
    [
        SpawnSetRoleDefinition.Vanilla(RoleTypeId.ChaosRepressor, count: 2, weight: 1.5f),
        SpawnSetRoleDefinition.Vanilla(RoleTypeId.ChaosMarauder, count: 2, weight: 1.5f),
        SpawnSetRoleDefinition.Vanilla(RoleTypeId.ChaosRifleman, count: 99, weight: 4f),
    ];

    protected override void OnSpawning()
    {
        Respawn.SummonChaosInsurgencyVan();
        base.OnSpawning();
    }
}
