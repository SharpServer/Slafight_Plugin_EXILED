using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs.Bases;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

/// <summary>
/// WaterWarriorsの触手。衝突は不可視 NPC 側で受ける。
/// </summary>
public class WaterTentacle : TentacleBase
{
    protected override string TentacleSchematicName => "WaterTentacle";
    protected override string HurtMessage => "水の触手に飲み込まれた";
    protected override CTeam ExcludedTeam => CTeam.Warriors;

    public override float MaxHealth { get; set; } = 5000f;
    public override float AttackRange { get; set; } = 3.85f;
    public override float AttackDamage { get; set; } = 75f;
    public override float AttackInterval { get; set; } = 1f;
    public override float StrikeDuration { get; set; } = 0.23f;
}
