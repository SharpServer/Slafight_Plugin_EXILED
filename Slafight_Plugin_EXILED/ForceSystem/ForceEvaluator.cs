using System;
using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.CustomEffects;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.API.Core.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.ForceSystem;

/// <summary>
/// 隊の状態を定期的に見直します。分隊の編成・解散・合流と、階級の昇進・昇格を担います。
/// </summary>
public static class ForceEvaluator
{
    /// <summary>本隊から離れて単独行動と見なす距離 (m)。</summary>
    private const float DetachDistance = 30f;

    /// <summary>単独行動どうしが分隊を組める距離 (m)。</summary>
    private const float SquadFormDistance = 15f;

    /// <summary>単独行動が既存の隊に合流できる距離 (m)。<see cref="DetachDistance"/> より小さくします。</summary>
    private const float JoinDistance = 15f;

    /// <summary>分隊が本隊に吸収され始める距離 (m)。</summary>
    private const float MergeDistance = 20f;

    /// <summary>吸収に必要な同行時間 (秒)。</summary>
    internal const float MergeSeconds = 60f;

    /// <summary>SubLead に昇進するのに必要な貢献度の内訳。</summary>
    private const float SubLeadShare = 0.70f;

    /// <summary>SubLead に昇進するのに必要な保持時間 (秒)。</summary>
    private const float SubLeadHoldSeconds = 60f;

    /// <summary>昇進条件が緩和されているときの内訳。</summary>
    private const float RelaxedShare = 0.60f;

    /// <summary>昇進条件が緩和されているときの保持時間 (秒)。</summary>
    private const float RelaxedHoldSeconds = 40f;

    /// <summary>
    /// この分隊が本隊に吸収されるまでの残り秒数です。寄り添っていなければ null。
    /// </summary>
    internal static float? MergeSecondsLeft(ForceBase squad)
    {
        if (squad is null || squad.IsMainForce) return null;

        return MergeSince.TryGetValue(squad, out float since)
            ? Mathf.Max(0f, MergeSeconds - (Time.time - since))
            : null;
    }

    /// <summary>Member から SubLead へ昇進できる最小の隊員数。</summary>
    internal const int MinMembersForPromotion = 3;

    /// <summary>Member から SubLead へ昇進できる最小の貢献度。</summary>
    internal const int MinContributionForPromotion = 30;

    /// <summary>編成の見直し間隔 (秒)。</summary>
    private const float StructureInterval = 2f;

    private static readonly float DetachDistanceSqr = DetachDistance * DetachDistance;
    private static readonly float SquadFormDistanceSqr = SquadFormDistance * SquadFormDistance;
    private static readonly float JoinDistanceSqr = JoinDistance * JoinDistance;
    private static readonly float MergeDistanceSqr = MergeDistance * MergeDistance;

    /// <summary>バフの持続時間 (秒)。tick より長くして、掛け直しの隙間を作らない。</summary>
    private const float BuffDuration = StructureInterval * 2.5f;



    /// <summary>
    /// 分隊が本隊に寄り添い始めた時刻です。離れたら消えます。
    /// </summary>
    private static readonly Dictionary<ForceBase, float> MergeSince = new();

    /// <summary>
    /// 評価ループを起こします。二重に呼んでも <see cref="RoundScope"/> 側で片付きます。
    /// </summary>
    public static void Start()
    {
        MergeSince.Clear();

        RoundScope.Current.RunLoop(StructureInterval, Tick);
        RoundScope.Current.OnEnd(MergeSince.Clear);
    }

    /// <summary>
    /// 1 回ぶんの見直しです。
    /// </summary>
    private static void Tick()
    {
        ForceRegistry.Prune();

        foreach (ForceBase force in ForceRegistry.All.ToArray())
        {
            EnsureTopLead(force);
            UpdateDetachment(force);
            UpdatePromotions(force);
        }

        JoinNearbyForces();
        FormSquads();
        UpdateMerges();
        PromoteSquads();
        ApplyBuffs();
        RefreshNameplates();
    }

    /// <summary>
    /// 隊員の名札を描き直します。
    /// </summary>
    private static void RefreshNameplates()
    {
        foreach (Player player in Player.List)
        {
            if (!player.IsSafePlayer() || player.GetForceMember() is null) continue;

            ForceNameplate.Refresh(player);
        }
    }

    // ───────────────────────────────
    // TopLead の選出
    // ───────────────────────────────

    /// <summary>
    /// 隊が作られた直後に TopLead と SubLead を役職優先度で決めます。
    /// </summary>
    internal static void AssignTopLead(ForceBase force)
    {
        if (force?.Members is not { Count: > 0 }) return;

        List<ForceMember> ranked =
        [
            .. force.Members
                .Where(member => member.IsAlive)
                .OrderByDescending(member => ForceRolePower.Of(member.Player))
        ];

        if (ranked.Count == 0) return;

        ForceMember lead = ranked[0];
        lead.Rank = ForceClassLevel.TopLead;

        int leadPower = ForceRolePower.Of(lead.Player);

        foreach (ForceMember member in ranked.Skip(1))
        {
            // 隊長より 1 段下の役職を補佐に据える。全員同格なら補佐は置かない。
            member.Rank = ForceRolePower.Of(member.Player) > 0 &&
                          ForceRolePower.Of(member.Player) < leadPower &&
                          ForceRolePower.Of(member.Player) >= leadPower - 1
                ? ForceClassLevel.SubLead
                : ForceClassLevel.Member;
        }
    }

    /// <summary>
    /// TopLead が居なくなっていたら次を立てます。
    /// </summary>
    private static void EnsureTopLead(ForceBase force)
    {
        if (force.TopLead is { IsAlive: true }) return;

        // まず補佐から。居なければ一般隊員から。
        ForceMember next = force.BestBy(member => member.Rank is ForceClassLevel.SubLead)
                           ?? force.BestBy(_ => true);

        if (next is null) return;

        next.Rank = ForceClassLevel.TopLead;
        Notify(next, force.PromotedToTopLeadText());
    }

    // ───────────────────────────────
    // 離脱と分隊の編成
    // ───────────────────────────────

    /// <summary>
    /// 本隊から離れすぎた隊員を外します。TopLead は外しません。
    /// </summary>
    private static void UpdateDetachment(ForceBase force)
    {
        if (force.TopLead is not { IsAlive: true } lead) return;

        Vector3 anchor = lead.Player.Position;

        foreach (ForceMember member in force.Members.ToArray())
        {
            if (!member.IsAlive || ReferenceEquals(member, lead)) continue;

            if ((member.Player.Position - anchor).sqrMagnitude <= DetachDistanceSqr) continue;

            force.Remove(member);
        }
    }

    /// <summary>
    /// 無所属の人を近くの隊に合流させます。本隊を優先します。
    /// </summary>
    private static void JoinNearbyForces()
    {
        foreach (Player player in Player.List)
        {
            if (!player.IsSafePlayer() || !player.IsAlive) continue;

            if (player.Role is null || !ForceKinds.IsForceTeam(player.Role.Team)) continue;

            if (ForceRegistry.MemberOf(player) is not { Force: null } member) continue;

            if (NearestJoinable(member) is not { } target) continue;

            // 隊長が既に居るところへ入るので、率いていた人は補佐に下がる。
            if (member.Rank is ForceClassLevel.TopLead)
                member.Rank = ForceClassLevel.SubLead;

            target.Add(member);
            Notify(member, $"{target.Name} に合流しました");
        }
    }

    /// <summary>
    /// この人が合流できる、いちばん近い隊です。無ければ null。
    /// </summary>
    private static ForceBase NearestJoinable(ForceMember member)
    {
        Type kind = ForceKinds.For(member.Player);

        if (kind is null) return null;

        Vector3 origin = member.Player.Position;
        ForceBase best = null;
        float bestSqr = JoinDistanceSqr;

        foreach (ForceBase force in ForceRegistry.All)
        {
            if (force.GetType() != kind || force.AliveCount == 0) continue;

            foreach (ForceMember other in force.Members)
            {
                if (!other.IsAlive || !other.Player.IsAlive) continue;

                float sqr = (other.Player.Position - origin).sqrMagnitude;

                if (sqr > bestSqr) continue;

                // 同距離なら本隊を優先する。
                if (best is not null && !force.IsMainForce && best.IsMainForce && sqr >= bestSqr) continue;

                best = force;
                bestSqr = sqr;
            }
        }

        return best;
    }

    /// <summary>
    /// 無所属どうしが近くに 2 人以上居たら分隊を組ませます。
    /// </summary>
    private static void FormSquads()
    {
        // 波で出ていないプレイヤー (ラウンド開始時の D クラスなど) はまだ隊員状態を持たない。
        // ここで作らないとギャングが一生編成されないので、対象陣営だけ作りに行く。
        List<ForceMember> loose =
        [
            .. Player.List
                .Where(player => player.IsSafePlayer() && player.IsAlive && ForceKinds.IsForceTeam(player.Role.Team))
                .Select(ForceRegistry.MemberOf)
                .Where(member => member is { Force: null, IsAlive: true })
                // 先に単独だった人から順に。Player.List の並び順で隊長が決まらないようにする。
                .OrderBy(member => member.AloneSince ?? 0f)
        ];

        while (loose.Count >= 2)
        {
            ForceMember seed = loose[0];
            loose.RemoveAt(0);

            List<ForceMember> nearby =
            [
                .. loose
                    .Where(member =>
                        (member.Player.Position - seed.Player.Position).sqrMagnitude <= SquadFormDistanceSqr)
            ];

            if (nearby.Count == 0) continue;

            ForceBase squad = CreateSquad(seed);

            if (squad is null) continue;

            // どの本隊から分かれたのかを決めてから登録する。
            // 親が決まっていないと HUD でぶら下がらず、番号も本隊ごとに振れない。
            squad.Parent = NearestParentFor(seed);

            squad.Add(seed);

            foreach (ForceMember member in nearby)
            {
                squad.Add(member);
                loose.Remove(member);
            }

            ForceRegistry.Register(squad);
            PromoteSquadLead(squad);

            foreach (ForceMember member in squad.Members)
                Notify(member, squad.SquadFormedText());
        }
    }

    /// <summary>
    /// この人が分かれてきたと見なせる、同じ種類でいちばん近い本隊です。無ければ null。
    /// </summary>
    private static ForceBase NearestParentFor(ForceMember seed)
    {
        Type kind = ForceKinds.For(seed.Player);

        if (kind is null) return null;

        Vector3 origin = seed.Player.Position;
        ForceBase best = null;
        float bestSqr = float.MaxValue;

        foreach (ForceBase force in ForceRegistry.All)
        {
            if (!force.IsMainForce || force.GetType() != kind || force.AliveCount == 0) continue;

            if (force.TopLead is not { IsAlive: true } lead) continue;

            float sqr = (lead.Player.Position - origin).sqrMagnitude;

            if (sqr >= bestSqr) continue;

            best = force;
            bestSqr = sqr;
        }

        return best;
    }

    /// <summary>
    /// 分隊の実体を作ります。種類は元の隊に揃えます。
    /// </summary>
    private static ForceBase CreateSquad(ForceMember seed)
    {
        ForceBase squad = ForceKinds.Create(seed.Player.Role.Team);

        if (squad is null) return null;

        squad.IsMainForce = false;

        return squad;
    }

    /// <summary>
    /// 分隊の隊長を選び直します。SubLead は保持します。
    /// </summary>
    private static void PromoteSquadLead(ForceBase squad)
    {
        foreach (ForceMember member in squad.Members)
        {
            if (member.Rank is ForceClassLevel.TopLead)
                member.Rank = ForceClassLevel.SubLead;
        }

        if (squad.BestBy(_ => true) is not { } best) return;

        best.Rank = ForceClassLevel.TopLead;
        Notify(best, squad.PromotedToTopLeadText());
    }

    // ───────────────────────────────
    // 合流
    // ───────────────────────────────

    /// <summary>
    /// 本隊に寄り添っている分隊を吸収します。
    /// </summary>
    private static void UpdateMerges()
    {
        foreach (ForceBase squad in ForceRegistry.All.Where(force => !force.IsMainForce).ToArray())
        {
            ForceBase main = NearestMainForce(squad);

            if (main is null)
            {
                MergeSince.Remove(squad);

                continue;
            }

            if (!MergeSince.TryGetValue(squad, out float since))
            {
                MergeSince[squad] = Time.time;

                continue;
            }

            if (Time.time - since < MergeSeconds) continue;

            Merge(squad, main);
            MergeSince.Remove(squad);
        }
    }

    /// <summary>
    /// この分隊が寄り添っている本隊です。居なければ null。
    /// </summary>
    private static ForceBase NearestMainForce(ForceBase squad)
    {
        if (squad.TopLead is not { IsAlive: true } squadLead) return null;

        foreach (ForceBase main in ForceRegistry.OfFaction(squad.Faction))
        {
            if (!main.IsMainForce || ReferenceEquals(main, squad)) continue;

            if (main.TopLead is not { IsAlive: true } mainLead) continue;

            if ((mainLead.Player.Position - squadLead.Player.Position).sqrMagnitude <= MergeDistanceSqr)
                return main;
        }

        return null;
    }

    /// <summary>
    /// 分隊を本隊に吸収します。分隊の TopLead は SubLead に降ります。
    /// </summary>
    private static void Merge(ForceBase squad, ForceBase main)
    {
        foreach (ForceMember member in squad.Members.ToArray())
        {
            if (member.Rank is ForceClassLevel.TopLead)
                member.Rank = ForceClassLevel.SubLead;

            main.Add(member);
            Notify(member, squad.SquadMergedText());
        }

        ForceRegistry.Dissolve(squad);
    }

    // ───────────────────────────────
    // 昇進
    // ───────────────────────────────

    /// <summary>
    /// 貢献度の内訳を保ち続けた一般隊員を SubLead に昇進させます。
    /// </summary>
    private static void UpdatePromotions(ForceBase force)
    {
        // 小規模な隊では下限を満たさないので昇進させない。
        if (force.AliveCount < MinMembersForPromotion) return;

        foreach (ForceMember member in force.Members.ToArray())
        {
            if (!member.IsAlive || member.Rank is not ForceClassLevel.Member) continue;

            if (member.Contribution < MinContributionForPromotion)
            {
                member.SubLeadHoldSince = null;

                continue;
            }

            float needShare = member.HasRelaxedPromotion ? RelaxedShare : SubLeadShare;
            float needHold = member.HasRelaxedPromotion ? RelaxedHoldSeconds : SubLeadHoldSeconds;

            if (ForceContribution.ShareOf(member) < needShare)
            {
                member.SubLeadHoldSince = null;

                continue;
            }

            member.SubLeadHoldSince ??= Time.time;

            if (Time.time - member.SubLeadHoldSince.Value < needHold) continue;

            member.Rank = ForceClassLevel.SubLead;
            member.SubLeadHoldSince = null;
            member.HasRelaxedPromotion = false;

            Notify(member, force.PromotedToSubLeadText());
        }
    }

    // ───────────────────────────────
    // 隊の昇格
    // ───────────────────────────────

    /// <summary>
    /// 本隊が消えた陣営で、いちばん貢献している分隊を本隊に昇格させます。
    /// </summary>
    private static void PromoteSquads()
    {
        // 陣営ではなく「同じ側の隊」ごとに見る。Faction.FoundationEnemy には
        // カオスと D クラスのギャングが両方入るので、陣営で括ると
        // カオスの本隊が生きているだけでギャングが昇格できなくなる。
        // 括り方は ForceVisibility の既定と揃えてある (CustomTeam があればそれ、無ければ型)。
        foreach (IGrouping<object, ForceBase> group in ForceRegistry.All.GroupBy(SideKeyOf).ToArray())
        {
            List<ForceBase> forces = [.. group];

            if (forces.Any(force => force.IsMainForce && force.AliveCount > 0)) continue;

            List<ForceBase> squads = [.. forces.Where(force => force.AliveCount > 0)];

            if (squads.Count == 0) continue;

            // SubLead を擁する隊を優先し、その中で貢献度が高い隊を選ぶ。
            // 草案どおり役職が最優先。「一番貢献度が高い分隊よりも
            // 隊長役職を有する分隊が本隊になることができます」。
            ForceBase promoted = squads
                .OrderByDescending(force => force.Members
                    .Where(member => member.IsAlive)
                    .Select(member => ForceRolePower.Of(member.Player))
                    .DefaultIfEmpty(0)
                    .Max())
                .ThenByDescending(force => force.Members.Any(member =>
                    member.IsAlive && member.Rank is ForceClassLevel.SubLead))
                .ThenByDescending(force => force.TotalContribution)
                .First();

            ForceMember lead = promoted.BestBy(member => member.Rank is ForceClassLevel.SubLead)
                               ?? promoted.BestBy(_ => true);

            if (lead is null) continue;

            promoted.IsMainForce = true;
            promoted.Parent = null;
            lead.Rank = ForceClassLevel.TopLead;

            // 昇格した者の次点には、以後の昇進条件を緩和する。
            // 草案の「60% を 40 秒保持で SubLead」がこれにあたる。
            ForceMember runnerUp = promoted.Members
                .Where(member => member.IsAlive && !ReferenceEquals(member, lead))
                .OrderByDescending(member => member.Contribution)
                .FirstOrDefault();

            if (runnerUp is { Rank: ForceClassLevel.Member })
                runnerUp.HasRelaxedPromotion = true;

            // 残った分隊はこの本隊の下に付け直す。
            foreach (ForceBase squad in squads.Where(force => !ReferenceEquals(force, promoted)))
                squad.Parent = promoted;

            foreach (ForceMember member in promoted.Members)
                Notify(member, promoted.SquadFormedText());

            Notify(lead, promoted.PromotedToTopLeadText());
        }
    }

    /// <summary>
    /// 「同じ側の隊」を束ねるキーです。<see cref="ForceVisibility"/> と同じ基準です。
    /// </summary>
    private static object SideKeyOf(ForceBase force) => force.Team ?? (object)force.GetType();

    // ───────────────────────────────
    // バフ
    // ───────────────────────────────

    /// <summary>
    /// 隊員にバフを配ります。tick より少し長い持続で掛け直すだけで、剥がしません。
    /// </summary>
    private static void ApplyBuffs()
    {
        foreach (ForceBase force in ForceRegistry.All.ToArray())
        {
            byte movement = force.MovementBoost();
            byte damage = force.DamageBoost();
            byte regeneration = force.Regeneration();

            foreach (ForceMember member in force.Members.ToArray())
            {
                if (!member.IsAlive || !member.Player.IsAlive) continue;

                Boost<MovementBoost>(member.Player, movement);
                Boost<DamageBoost>(member.Player, damage);
                Boost<NaturalHeal>(member.Player, regeneration);
            }
        }
    }

    /// <summary>
    /// 強さが上がるときだけエフェクトを掛け直します。
    /// </summary>
    private static void Boost<T>(Player player, byte intensity) where T : StatusEffectBase
    {
        if (intensity == 0) return;

        if (player.TryGetEffect(out T existing) && existing.IsEnabled && existing.Intensity > intensity)
            return;

        player.EnableEffect<T>(intensity, BuffDuration);
    }

    /// <summary>
    /// 本人にだけ短く知らせます。
    /// </summary>
    private static void Notify(ForceMember member, string text)
    {
        if (!member.IsAlive || string.IsNullOrEmpty(text)) return;

        member.Player.ShowHint(text, 4f);
    }
}
