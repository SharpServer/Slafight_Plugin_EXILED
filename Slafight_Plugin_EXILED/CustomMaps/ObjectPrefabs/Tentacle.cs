using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs.Bases;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

/// <summary>
/// SCP-035 の触手。見た目は以前の Tentacle schematic を使い、衝突は不可視 NPC 側で受ける。
/// </summary>
public class Tentacle : TentacleBase
{
    protected override string TentacleSchematicName => "Tentacle";
    protected override string HurtMessage => "SCP-035の触手に襲われた";
    protected override CTeam ExcludedTeam => CTeam.SCPs;

    public override float MaxHealth { get; set; } = 1000f;
    public override float AttackRange { get; set; } = 1.85f;
    public override float AttackDamage { get; set; } = 35f;
    public override float AttackInterval { get; set; } = 2.5f;
    public override float StrikeDuration { get; set; } = 0.83f;
}
