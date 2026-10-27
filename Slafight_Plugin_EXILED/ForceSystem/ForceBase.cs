using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Enums;
using PlayerRoles;
using UnityEngine;

namespace Slafight_Plugin_EXILED.ForceSystem;

/// <summary>
/// 「隊」1 つを表します。本隊と分隊は <see cref="IsMainForce"/> で区別します。
/// </summary>
public abstract class ForceBase
{
    private readonly List<ForceMember> members = [];

    private string name;

    private int issuedSquads;

    /// <summary>
    /// この隊の表示名 (<c>ALPHA-01</c> など) です。初回参照時に決まり、以後は変わりません。
    /// </summary>
    public string Name => name ??= ForceNaming.Sanitize(IsMainForce ? BuildMainName() : BuildSquadName());

    /// <summary>
    /// 同じ種類・区分の中での通し番号です。1 始まり。分隊は親の本隊ごとに数えます。
    /// </summary>
    public int Ordinal { get; internal set; }

    /// <summary>
    /// この隊から分かれた分隊に配る次の番号です。
    /// </summary>
    /// <remarks>解散しても番号を再利用しないよう、作った数を数え続けます。</remarks>
    internal int NextSquadOrdinal() => ++issuedSquads;

    /// <summary>
    /// 本隊の名前を作ります。
    /// </summary>
    /// <remarks>既定はバニラと同じ <c>ALPHA-01</c> 形式です。</remarks>
    protected virtual string BuildMainName() => ForceNaming.IssueLocalName();

    /// <summary>
    /// 分隊の名前を作ります。既定は本隊と同じ形式です。返り値は自動で無害化されます。
    /// </summary>
    protected virtual string BuildSquadName() => ForceNaming.IssueLocalName();

    /// <summary>
    /// バニラの部隊番号です。NTF の本隊だけが持ち、それ以外は null。
    /// </summary>
    public abstract byte? UnitId { get; }

    /// <summary>
    /// この隊が属する陣営です。同じ側かどうかの判定には <see cref="ForceVisibility"/> を使ってください。
    /// </summary>
    public abstract Faction Faction { get; }

    /// <summary>
    /// この隊が属する <see cref="CustomTeam"/> です。名乗らなければ null。表示の仕分けにだけ使います。
    /// </summary>
    public virtual CustomTeam Team => null;

    /// <summary>
    /// 本隊かどうか。false なら分隊です。
    /// </summary>
    public bool IsMainForce { get; internal set; } = true;

    /// <summary>
    /// 分隊のとき、元になった本隊です。本隊なら null。本隊が消えると null に戻ります。
    /// </summary>
    public ForceBase Parent { get; internal set; }

    /// <summary>
    /// この隊の隊員です。
    /// </summary>
    public IReadOnlyList<ForceMember> Members => members;

    /// <summary>
    /// この隊を率いている隊員です。居なければ null。
    /// </summary>
    public ForceMember TopLead => members.FirstOrDefault(member => member.Rank is ForceClassLevel.TopLead);

    /// <summary>
    /// この隊が作られた時刻です (<see cref="Time.time"/> 基準)。
    /// </summary>
    public float CreatedAt { get; } = Time.time;

    /// <summary>
    /// 生きている隊員の数です。
    /// </summary>
    public int AliveCount => members.Count(member => member.IsAlive);

    /// <summary>
    /// この隊の貢献度の合計です。
    /// </summary>
    public int TotalContribution => members.Sum(member => member.Contribution);

    // ───────────────────────────────
    // 呼称
    //
    // 表示層はここしか読みません。文言を変えたい派生はこれを override します。
    // ───────────────────────────────

    /// <summary>隊長級の呼称です。</summary>
    public virtual string TopLeadName => "隊長";

    /// <summary>補佐級の呼称です。</summary>
    public virtual string SubLeadName => "補佐";

    /// <summary>一般隊員の呼称です。</summary>
    public virtual string MemberName => "隊員";

    /// <summary>隊から外れて単独行動している状態の呼称です。</summary>
    public virtual string AloneName => "単独行動";

    /// <summary>本隊の呼称です。</summary>
    public virtual string MainForceName => "本隊";

    /// <summary>分隊の呼称です。</summary>
    public virtual string SquadName => "分隊";

    /// <summary>
    /// この隊が本隊か分隊かの呼称です。
    /// </summary>
    public string KindName => IsMainForce ? MainForceName : SquadName;

    /// <summary>
    /// 階級の呼称を引きます。
    /// </summary>
    public string RankNameOf(ForceClassLevel level) => level switch
    {
        ForceClassLevel.TopLead => TopLeadName,
        ForceClassLevel.SubLead => SubLeadName,
        ForceClassLevel.Alone => AloneName,
        _ => MemberName,
    };

    /// <summary>TopLead に昇格したときの通知文です。</summary>
    public virtual string PromotedToTopLeadText() => $"<color={ServerColors.Yellow}>{TopLeadName}</color>に昇格しました";

    /// <summary>SubLead に昇進したときの通知文です。</summary>
    public virtual string PromotedToSubLeadText() => $"<color={ServerColors.Cyan}>{SubLeadName}</color>に昇進しました";

    /// <summary>分隊を編成したときの通知文です。</summary>
    public virtual string SquadFormedText() => $"{SquadName} <b>{Name}</b> を編成しました";

    /// <summary>分隊が本隊に吸収されたときの通知文です。</summary>
    public virtual string SquadMergedText() => $"{MainForceName}に合流しました";

    // ───────────────────────────────
    // 貢献度のルール
    //
    // 草案の派生システムはここの差分として表現します。
    // ───────────────────────────────

    /// <summary>
    /// 隊に居続けることの評価です。
    /// </summary>
    /// <remarks>草案の「分隊/本隊での継続所属時間 (影響：大)」。</remarks>
    public virtual ForceImpact MembershipImpact => ForceImpact.Large;

    /// <summary>
    /// 隊内外への継続的なやり取りの評価です。
    /// </summary>
    /// <remarks>草案の「継続的な隊内外に対してのコミュニケーション (影響：小)」。</remarks>
    public virtual ForceImpact CommunicationImpact => ForceImpact.Small;

    /// <summary>
    /// 非武装の敵を即座に射殺したことの減点です。
    /// </summary>
    /// <remarks>草案の「非武装の敵陣営に対する即射殺 (影響：中)」。</remarks>
    public virtual ForceImpact ExecutionPenalty => ForceImpact.Medium;

    /// <summary>
    /// 意図的な同士討ちの減点です。
    /// </summary>
    /// <remarks>草案の「意図的な FF (影響：大)」。</remarks>
    public virtual ForceImpact FriendlyFirePenalty => ForceImpact.Large;

    /// <summary>
    /// SCP-914 にキーカードを通したことの減点です。
    /// </summary>
    /// <remarks>草案の「SCP-914 でのキーカードの使用 (影響：小)」。</remarks>
    public virtual ForceImpact Scp914KeycardPenalty => ForceImpact.Small;

    /// <summary>
    /// SCP-914 にキーカードを通したときの加点です。既定は加点なし。
    /// </summary>
    public virtual ForceImpact Scp914KeycardReward => ForceImpact.None;

    /// <summary>
    /// キーカードを拾ったことの加点です。
    /// </summary>
    /// <remarks>標準では加点しません。D クラスのギャングだけが上書きします。</remarks>
    public virtual ForceImpact KeycardPickupReward => ForceImpact.None;

    // ───────────────────────────────
    // バフ
    //
    // 隊員の数で強さが変わります。分隊は本隊より弱く、SubLead が居ると強化されます。
    // ───────────────────────────────

    /// <summary>
    /// この隊が配る移動速度上昇の強さです。0 なら付けません。
    /// </summary>
    public virtual byte MovementBoost()
    {
        if (AliveCount < 2) return 0;

        int count = Mathf.Min(AliveCount, IsMainForce ? 5 : 4);
        int value = IsMainForce ? count * 2 : count;

        // 分隊は補佐が居ると強化される。
        if (!IsMainForce && HasSubLead) value += 2;

        return (byte)Mathf.Max(0, value);
    }

    /// <summary>
    /// 補佐が在籍しているかどうか。
    /// </summary>
    public bool HasSubLead =>
        members.Any(member => member.IsAlive && member.Rank is ForceClassLevel.SubLead);

    /// <summary>
    /// この隊が配る自然回復の強さです。0 なら付けません。
    /// </summary>
    public virtual byte Regeneration()
    {
        int need = IsMainForce ? 6 : 4;

        if (AliveCount < need) return 0;

        return (byte)(IsMainForce ? 2 : 1);
    }

    /// <summary>
    /// この隊が配る攻撃力上昇の強さです。0 なら付けません。
    /// </summary>
    public virtual byte DamageBoost()
    {
        if (IsMainForce)
            return AliveCount < 4 ? (byte)0 : (byte)Mathf.Min(2, AliveCount / 2);

        return HasSubLead ? (byte)1 : (byte)0;
    }

    // ───────────────────────────────
    // 隊員の出入り
    // ───────────────────────────────

    /// <summary>
    /// この人がいまの役職のままこの隊に居られるかどうか。false なら隊から外されます。
    /// </summary>
    public virtual bool Accepts(Player player) =>
        player is not null && ForceKinds.For(player) == GetType();

    /// <summary>
    /// 隊員を加えます。既に別の隊に属していれば、そちらから外します。
    /// </summary>
    internal void Add(ForceMember member)
    {
        if (member is null || members.Contains(member)) return;

        member.Force?.Remove(member);

        members.Add(member);
        member.Force = this;
        member.JoinedAt = Time.time;
        member.AloneSince = null;
    }

    /// <summary>
    /// 隊員を外します。
    /// </summary>
    internal void Remove(ForceMember member)
    {
        if (member is null || !members.Remove(member)) return;

        if (ReferenceEquals(member.Force, this))
        {
            member.Force = null;
            member.AloneSince = Time.time;
        }
    }

    /// <summary>
    /// 退出・死亡などで実体が失われた隊員を掃除します。
    /// </summary>
    internal void PruneDead()
    {
        for (int index = members.Count - 1; index >= 0; index--)
        {
            ForceMember member = members[index];

            if (member.IsAlive && member.Player.IsAlive) continue;

            members.RemoveAt(index);

            if (ReferenceEquals(member.Force, this))
            {
                member.Force = null;
                member.AloneSince = Time.time;
            }
        }
    }

    /// <summary>
    /// この隊の中で貢献度がいちばん高い、条件に合う隊員です。
    /// </summary>
    internal ForceMember BestBy(System.Func<ForceMember, bool> predicate) =>
        members.Where(member => member.IsAlive && predicate(member))
            .OrderByDescending(member => ForceRolePower.Of(member.Player))
            .ThenByDescending(member => member.Contribution)
            .ThenBy(member => member.AloneSince ?? 0f)
            .FirstOrDefault();
}
