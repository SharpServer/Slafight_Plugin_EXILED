namespace Slafight_Plugin_EXILED.ForceSystem;

/// <summary>
/// 貢献度の増減幅です (大 10 / 中 5 / 小 1)。
/// </summary>
public enum ForceImpact
{
    /// <summary>影響なし。派生システムが特定のルールを無効化するときに使います。</summary>
    None = 0,

    /// <summary>影響：小。</summary>
    Small = 1,

    /// <summary>影響：中。</summary>
    Medium = 5,

    /// <summary>影響：大。</summary>
    Large = 10,
}

/// <summary>
/// 隊員ごとの貢献度の出入りをまとめます。
/// </summary>
public static class ForceContribution
{
    /// <summary>
    /// 貢献度を加えます。負の値なら減点です。0 未満にはなりません。
    /// </summary>
    public static void Add(ForceMember member, int amount)
    {
        if (member is null || amount == 0) return;

        member.Contribution = System.Math.Max(0, member.Contribution + amount);
    }

    /// <summary>
    /// 加点します。
    /// </summary>
    public static void Reward(ForceMember member, ForceImpact impact) => Add(member, (int)impact);

    /// <summary>
    /// 減点します。
    /// </summary>
    public static void Penalize(ForceMember member, ForceImpact impact) => Add(member, -(int)impact);

    /// <summary>
    /// 隊全体の貢献度に占める、この隊員の割合です。隊が空なら 0。
    /// </summary>
    public static float ShareOf(ForceMember member)
    {
        if (member?.Force is not { } force) return 0f;

        int total = force.TotalContribution;

        return total <= 0 ? 0f : (float)member.Contribution / total;
    }
}
