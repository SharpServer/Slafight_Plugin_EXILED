using PlayerRoles;

namespace Slafight_Plugin_EXILED.ForceSystem.Forces;

/// <summary>
/// カオス・インサージェンシーの隊です。SCP-914 で減点されず、継続所属の影響が「中」です。
/// </summary>
public sealed class ChaosForce : ForceBase
{
    /// <summary>
    /// カオス側にバニラの部隊名はありません。
    /// </summary>
    public override byte? UnitId => null;

    /// <inheritdoc />
    public override Faction Faction => Faction.FoundationEnemy;

    /// <summary>
    /// 継続所属時間の影響は「中」に抑えられます。
    /// </summary>
    /// <remarks>草案「継続所属時間の影響が中に抑えられています」。</remarks>
    public override ForceImpact MembershipImpact => ForceImpact.Medium;

    /// <summary>
    /// SCP-914 の使用は減点対象から外れます。
    /// </summary>
    /// <remarks>草案「SCP-914 の使用が減点対象から外れ」。</remarks>
    public override ForceImpact Scp914KeycardPenalty => ForceImpact.None;

    /// <summary>
    /// 分隊は通し番号で名乗ります。
    /// </summary>
    /// <remarks>本隊は既定どおり NATO 名です。</remarks>
    protected override string BuildSquadName() => $"第{Ordinal}{SquadName}";
}
