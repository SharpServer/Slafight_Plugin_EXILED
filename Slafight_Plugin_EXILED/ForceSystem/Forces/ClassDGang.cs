using PlayerRoles;

namespace Slafight_Plugin_EXILED.ForceSystem.Forces;

/// <summary>
/// D クラス職員のギャングです。悪名高い行いが加点になり、継続所属の影響が「小」です。
/// </summary>
public sealed class ClassDGang : ForceBase
{
    /// <inheritdoc />
    public override byte? UnitId => null;

    /// <inheritdoc />
    public override Faction Faction => Faction.FoundationEnemy;

    // 呼称。草案が派生システムの違いとして「部隊の呼称の仕方」を挙げているところ。

    /// <inheritdoc />
    public override string TopLeadName => "ボス";

    /// <inheritdoc />
    public override string SubLeadName => "幹部";

    /// <inheritdoc />
    public override string MemberName => "構成員";

    /// <inheritdoc />
    public override string AloneName => "はぐれ者";

    /// <inheritdoc />
    public override string MainForceName => "ギャング";

    /// <inheritdoc />
    public override string SquadName => "分派";

    // 貢献度。草案「キーカードを拾ったり、914 をいじりまくったりなど
    // 悪名高いことをしていくことで貢献度を稼ぐことが可能で
    // 継続所属時間の影響が小に抑えられ」。

    /// <inheritdoc />
    public override ForceImpact MembershipImpact => ForceImpact.Small;

    /// <summary>
    /// SCP-914 にキーカードを通すのは減点ではなく加点です。
    /// </summary>
    public override ForceImpact Scp914KeycardPenalty => ForceImpact.None;

    /// <inheritdoc />
    public override ForceImpact Scp914KeycardReward => ForceImpact.Small;

    /// <inheritdoc />
    public override ForceImpact KeycardPickupReward => ForceImpact.Small;

    /// <inheritdoc />
    protected override string BuildSquadName() => $"第{Ordinal}{SquadName}";
}
