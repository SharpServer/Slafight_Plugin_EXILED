#nullable enable
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.ForceSystem;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Hints;

/// <summary>
/// 右上の部隊表示を組みます。
/// </summary>
/// <remarks>
/// <b>バニラの部隊名 HUD はサーバーから触れません。</b>
/// <c>Respawning.NamingRules.UnitNamingHud</c> はサーバー側アセンブリでは本体が空の
/// クライアント専用 MonoBehaviour なので、こちらで描き直すしかありません。
///
/// 何が見えるかは <see cref="ForceVisibility"/> が決めます。既定では
/// <see cref="ForceBase.Team"/> (無ければ隊の型) が同じ側の隊だけが並びます。
///
/// 表示の方針は<b>「部隊システムが今なにをしているかが読み取れること」</b>です。
/// 隊名と人数だけだと、バフが乗っているのか・分隊が組めるのかが分からず、
/// 動いていないのと区別が付きません。そこで自分の状態・受けている効果・
/// 次に何が起きるかを併記します。
/// </remarks>
public static class ForceHud
{
    /// <summary>隊の一覧に並べる最大行数です。</summary>
    private const int MaxForceRows = 6;

    /// <summary>自分の隊を強調する色です。</summary>
    private const string SelfColor = "#ffd700";

    /// <summary>他の隊の色です。</summary>
    private const string OtherColor = "#c8c8c8";

    /// <summary>補足情報の色です。</summary>
    private const string MutedColor = "#9a9a9a";

    /// <summary>バフ表示の色です。</summary>
    private const string BuffColor = "#7dff9b";

    /// <summary>警告の色です。</summary>
    private const string WarnColor = "#ff8f5b";

    /// <summary>
    /// 名前をそのまま出せる形にします。
    /// </summary>
    /// <remarks>
    /// ニックネームにタグの記号が入っているとリッチテキストとして解釈され、
    /// 以降の行まで崩れます。全角に置き換えて無害化します。
    /// </remarks>
    private static string Safe(string? text) =>
        string.IsNullOrEmpty(text) ? string.Empty : text!.Replace("<", "＜").Replace(">", "＞");

    /// <summary>
    /// このプレイヤーに見せる行を返します。何も無ければ空の一覧。
    /// </summary>
    /// <remarks>
    /// 1 行 = 1 Hint に分けて描くため、連結済みの 1 文字列ではなく行の一覧を返します。
    /// <c>HintAlignment.Right</c> は解像度によって位置が動くので使いません。
    /// </remarks>
    public static IReadOnlyList<string> Rows(Player? viewer)
    {
        if (viewer is null || viewer.Role is null) return [];

        // 部隊システムの対象外 (SCP・科学者) には何も出さない。
        if (!ForceKinds.IsForceTeam(viewer.Role.Team)) return [];

        ForceMember? member = viewer.GetForceMember();
        ForceBase? own = member?.Force;

        List<string> rows =
        [
            Header(own),
            .. own is null ? LooseBlock(viewer) : SelfBlock(viewer, member!, own)
        ];

        List<ForceBase> visible =
        [
            .. ForceVisibility.VisibleTo(viewer)
                .Where(force => force.AliveCount > 0)
        ];

        if (visible.Count > 0)
            rows.AddRange(ForceList(visible, own));

        return rows;
    }

    /// <summary>
    /// 見出しです。常に出して「部隊システムが動いている」ことを示します。
    /// </summary>
    private static string Header(ForceBase? own)
    {
        string label = own?.KindName ?? "部隊";

        return $"<size=20><color={MutedColor}>━━ {label} ━━</color></size>";
    }

    // ───────────────────────────────
    // 自分の状態
    // ───────────────────────────────

    /// <summary>
    /// 隊に属しているときの自分の状態です。
    /// </summary>
    private static IEnumerable<string> SelfBlock(Player viewer, ForceMember member, ForceBase force)
    {
        string kind = force.IsMainForce ? force.MainForceName : force.SquadName;

        yield return $"<color={MutedColor}><size=18>所属</size></color> " +
                     $"<color={SelfColor}><b>{force.Name}</b></color> " +
                     $"<size=19><color={OtherColor}>{kind} {force.AliveCount}名</color></size>";

        int share = Mathf.RoundToInt(ForceContribution.ShareOf(member) * 100f);

        yield return $"<color={MutedColor}><size=18>階級</size></color> " +
                     $"<size=20>{force.RankNameOf(member.Level)}</size> " +
                     $"<size=18><color={MutedColor}>貢献 {member.Contribution} ({share}%)</color></size>";

        if (Composition(force) is { Length: > 0 } composition)
            yield return $"<color={MutedColor}><size=18>編成</size></color> " +
                         $"<size=18><color={OtherColor}>{composition}</color></size>";

        yield return $"<color={MutedColor}><size=18>効果</size></color> " +
                     $"<size=18><color={BuffColor}>{Buffs(force)}</color></size>";

        if (LeadDistance(viewer, force) is { Length: > 0 } distance)
            yield return $"<size=18>{distance}</size>";

        if (NextPromotion(member, force) is { Length: > 0 } promotion)
            yield return $"<size=18><color={MutedColor}>{promotion}</color></size>";

        if (MergeCountdown(force) is { Length: > 0 } merge)
            yield return $"<size=18>{merge}</size>";
    }

    /// <summary>
    /// 本隊に吸収されるまでの残り時間です。寄り添っていなければ空文字。
    /// </summary>
    private static string MergeCountdown(ForceBase force)
    {
        if (ForceEvaluator.MergeSecondsLeft(force) is not { } left) return string.Empty;

        return $"<color={BuffColor}>{force.MainForceName}へ合流まで {Mathf.CeilToInt(left)}秒</color>";
    }

    /// <summary>
    /// 隊に属していないときの案内です。
    /// </summary>
    /// <remarks>
    /// 「何をすれば隊に入れるか」を出します。ここが空白だと、
    /// 単に部隊システムが動いていないのか、自分が外れているだけなのか区別が付きません。
    /// </remarks>
    private static IEnumerable<string> LooseBlock(Player viewer)
    {
        yield return $"<color={MutedColor}>単独行動中</color>";

        // 近くに隊があるならそちらへ合流できる。単独どうしなら分隊を組む。
        ForceBase joinable = ForceVisibility.VisibleTo(viewer)
            .FirstOrDefault(force => force.AliveCount > 0 && force.Members.Any(other =>
                other.IsAlive && other.Player.IsAlive &&
                Vector3.Distance(other.Player.Position, viewer.Position) <= 15f));

        if (joinable is not null)
        {
            yield return $"<size=18><color={BuffColor}>{joinable.Name} に合流できます</color></size>";

            yield break;
        }

        int nearby = Player.List.Count(other =>
            other.IsSafePlayer() && other.IsAlive && !ReferenceEquals(other, viewer) &&
            other.GetForceMember() is { Force: null } &&
            ForceKinds.For(other) == ForceKinds.For(viewer) &&
            Vector3.Distance(other.Position, viewer.Position) <= 15f);

        yield return nearby > 0
            ? $"<size=18><color={BuffColor}>近くに単独 {nearby}名 ・まもなく分隊を編成</color></size>"
            : $"<size=18><color={MutedColor}>味方が近づくと分隊を編成 / 隊に合流</color></size>";
    }

    /// <summary>
    /// 階級の内訳です。
    /// </summary>
    private static string Composition(ForceBase force)
    {
        int subLead = force.Members.Count(m => m.IsAlive && m.Rank is ForceClassLevel.SubLead);
        int plain = force.Members.Count(m => m.IsAlive && m.Rank is ForceClassLevel.Member);

        List<string> parts = [];

        if (force.TopLead is { IsAlive: true })
            parts.Add($"{force.TopLeadName} 1");

        if (subLead > 0) parts.Add($"{force.SubLeadName} {subLead}");
        if (plain > 0) parts.Add($"{force.MemberName} {plain}");

        return parts.Count == 0 ? string.Empty : string.Join(" / ", parts);
    }

    /// <summary>
    /// いま受けている隊バフです。
    /// </summary>
    /// <remarks>
    /// <b>これが「部隊システムがちゃんと効いているか」の答えになります。</b>
    /// 数値が出ていれば効果が乗っている、0 なら乗っていないと一目で分かります。
    /// </remarks>
    private static string Buffs(ForceBase force)
    {
        List<string> parts = [];

        if (force.MovementBoost() is > 0 and var movement) parts.Add($"移動+{movement}");
        if (force.DamageBoost() is > 0 and var damage) parts.Add($"攻撃+{damage}");
        if (force.Regeneration() is > 0 and var heal) parts.Add($"回復+{heal}");

        return parts.Count == 0 ? "なし (2名から発動)" : string.Join(" ", parts);
    }

    /// <summary>
    /// 隊長までの距離です。自分が隊長なら空文字。
    /// </summary>
    /// <remarks>
    /// 離脱しかけていることが分かるよう、しきい値に近づいたら警告色にします。
    /// </remarks>
    private static string LeadDistance(Player viewer, ForceBase force)
    {
        if (force.TopLead is not { IsAlive: true } lead) return string.Empty;

        if (ReferenceEquals(lead.Player, viewer)) return string.Empty;

        int metres = Mathf.RoundToInt(Vector3.Distance(viewer.Position, lead.Player.Position));

        // 離脱は 30m。近づいてきたら知らせる。
        return metres >= 24
            ? $"<color={WarnColor}>{force.TopLeadName}まで {metres}m ・離れすぎ注意</color>"
            : $"{force.TopLeadName}まで {metres}m";
    }

    /// <summary>
    /// 次の昇進までの案内です。
    /// </summary>
    private static string NextPromotion(ForceMember member, ForceBase force)
    {
        if (member.Rank is not ForceClassLevel.Member) return string.Empty;

        if (force.AliveCount < ForceEvaluator.MinMembersForPromotion)
            return $"{force.SubLeadName}昇進は {ForceEvaluator.MinMembersForPromotion}名から";

        if (member.Contribution < ForceEvaluator.MinContributionForPromotion)
            return $"{force.SubLeadName}まで 貢献 {ForceEvaluator.MinContributionForPromotion} " +
                   $"(現在 {member.Contribution})";

        int need = Mathf.RoundToInt((member.HasRelaxedPromotion ? 0.60f : 0.70f) * 100f);
        int now = Mathf.RoundToInt(ForceContribution.ShareOf(member) * 100f);

        return now >= need
            ? $"<color={BuffColor}>{force.SubLeadName}へ昇進中…</color>"
            : $"{force.SubLeadName}まで 貢献 {need}% (現在 {now}%)";
    }

    // ───────────────────────────────
    // 隊の一覧
    // ───────────────────────────────

    /// <summary>
    /// 見えている隊を並べます。
    /// </summary>
    private static IEnumerable<string> ForceList(List<ForceBase> visible, ForceBase? own)
    {
        yield return $"<size=18><color={MutedColor}>──────────</color></size>";

        List<ForceBase> mains =
        [
            .. visible
                .Where(force => force.IsMainForce)
                .OrderByDescending(force => ReferenceEquals(force, own))
                .ThenByDescending(force => force.AliveCount)
        ];

        int shown = 0;

        foreach (ForceBase main in mains)
        {
            if (shown >= MaxForceRows) break;

            yield return ForceRow(main, own, indent: false);
            shown++;

            foreach (ForceBase squad in visible.Where(s => ReferenceEquals(s.Parent, main)))
            {
                if (shown >= MaxForceRows) break;

                yield return ForceRow(squad, own, indent: true);
                shown++;
            }
        }

        // 親を失った分隊も出す。隊の昇格の候補なので隠すと状況が読めない。
        foreach (ForceBase orphan in visible.Where(f => !f.IsMainForce && f.Parent is null))
        {
            if (shown >= MaxForceRows) break;

            yield return ForceRow(orphan, own, indent: true);
            shown++;
        }

        if (visible.Count > shown)
            yield return $"<size=18><color={MutedColor}>… 他 {visible.Count - shown} 隊</color></size>";
    }

    /// <summary>
    /// 隊 1 つぶんの 1 行です。
    /// </summary>
    private static string ForceRow(ForceBase force, ForceBase? own, bool indent)
    {
        bool isOwn = ReferenceEquals(force, own);
        string color = isOwn ? SelfColor : OtherColor;
        string prefix = indent ? $"<size=18><color={MutedColor}>└ {force.SquadName}</color></size> " : string.Empty;
        string name = isOwn ? $"<b>{force.Name}</b>" : force.Name;
        string lead = force.TopLead is { IsAlive: true } topLead
            ? $" <size=16><color={MutedColor}>{Safe(topLead.Player.Nickname)}</color></size>"
            : $" <size=16><color={WarnColor}>指揮官不在</color></size>";

        return $"{prefix}<color={color}>{name}</color> " +
               $"<size=18><color={OtherColor}>{force.AliveCount}名</color></size>{lead}";
    }
}
