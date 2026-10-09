using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Extensions;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.ForceSystem;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// 何人に、どの役職を、何人ずつ割り当てるかをまとめて宣言するものです。
/// ラウンド開始時の一括割り当てや、イベント開始時の陣営配布に使います。
/// </summary>
/// <remarks>
/// <b>これは旧 <c>MainHandlers/SpawnSystem</c> 一式を置き換えるためのものです。</b>
/// 波を表す中間基底クラスや <c>SpawnTypeId</c> のような enum を挟まないでください。
/// 波は <see cref="SpawnSet"/> の派生クラスそのものとして宣言し、
/// 陣営・重み・比率・テーマはその派生クラスのプロパティとして持たせます。
/// enum と引き表に分けた瞬間、旧構造へ逆戻りします。
/// </remarks>
/// <example>
/// <code>
/// public class ChaosRaidSet : SpawnSet
/// {
///     public override string Name => "Chaos Raid";
///
///     public override int AllowedPlayerCount => 6;
///
///     public override IReadOnlyList&lt;SpawnSetRoleDefinition&gt; SpawnRoles =>
///     [
///         SpawnSetRoleDefinition.Custom&lt;ChaosCommando&gt;(count: 1, isForced: true),
///         SpawnSetRoleDefinition.Vanilla(RoleTypeId.ChaosRifleman, count: 99),
///     ];
/// }
/// </code>
/// </example>
public abstract class SpawnSet
{
    /// <summary>
    /// <see cref="SpawnFor"/> の間だけ差し替わる対象一覧です。
    /// </summary>
    private List<Player> forcedCandidates;

    /// <summary>
    /// 表示名です。
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// 説明です。
    /// </summary>
    public virtual string Description => string.Empty;

    /// <summary>
    /// この SpawnSet が割り当てる最大人数です。
    /// -1 なら対象プレイヤーが居る限り割り当てます。
    /// </summary>
    public virtual int AllowedPlayerCount => -1;

    /// <summary>
    /// 全員をこの位置に飛ばします。null なら役職側の指定に任せます。
    /// </summary>
    public virtual Vector3? OverridePosition => null;

    /// <summary>
    /// 割り当てる役職の一覧です。
    /// </summary>
    public abstract IReadOnlyList<SpawnSetRoleDefinition> SpawnRoles { get; }

    /// <summary>
    /// 役職を重み抽選ではなく、行ごとに 1 人ずつ順番に配るかどうか。
    /// </summary>
    /// <remarks>
    /// 人数が少なくても各役職が 1 人ずつ揃うので、編成を安定させたい波に向きます。
    /// <see cref="SpawnSetRoleDefinition.Weight"/> は無視され、
    /// <see cref="SpawnSetRoleDefinition.Count"/> の上限だけが効きます。
    /// </remarks>
    public virtual bool RoundRobin => false;

    // ───────────────────────────────
    // リスポーンウェーブとして使うときの宣言
    //
    // 波を表す中間基底クラスも、波を指す enum も作りません。
    // 波は SpawnSet の派生クラスそのもので、下の virtual を override して名乗ります。
    // ラウンド開始時の一括割り当てのように波ではない SpawnSet は、何も override しません。
    // ───────────────────────────────

    /// <summary>
    /// この波が属する陣営です。<see cref="RespawnWeight"/> が 0 なら使われません。
    /// </summary>
    public virtual Faction RespawnFaction => Faction.Unclassified;

    /// <summary>
    /// 本隊ではなく増援 (ミニウェーブ) かどうか。
    /// </summary>
    public virtual bool IsMiniWave => false;

    /// <summary>
    /// リスポーン抽選での重みです。
    /// <b>既定の 0 は「抽選に出ない」= 波ではない、という意味です。</b>
    /// 波として使いたい SpawnSet だけがここを override します。
    /// </summary>
    public virtual int RespawnWeight => 0;

    /// <summary>
    /// 待機者のうち実際に出す割合です。1 なら全員。
    /// </summary>
    public virtual float RespawnRatio => 1f;

    /// <summary>
    /// 出撃時に流す曲のパスです。null なら無音。
    /// </summary>
    public virtual string Theme => null;

    /// <summary>
    /// 出撃時のアナウンスです。既定は無し。
    /// </summary>
    /// <remarks>
    /// 旧実装はこれを 24 分岐の switch 2 つで引いていました。波が自分で名乗れば表は要りません。
    /// </remarks>
    /// <param name="spawnCount">実際に出た人数。</param>
    /// <param name="unitName">
    /// この波に付いた部隊名 (<c>ALPHA-01</c> 形式)。FoundationStaff の波でなければ null。
    /// 読み上げに混ぜたい場合は <c>UnitNamingRule.TranslateToCassie</c> で
    /// CASSIE 用の綴り (<c>NATO_A 01</c>) に直してください。
    /// </param>
    public virtual (string Cassie, string Subtitle) Announcement(int spawnCount, string unitName) => default;

    /// <summary>
    /// この波が作る隊です。null なら陣営から自動で決まります。
    /// </summary>
    /// <remarks>
    /// ほとんどの波は override 不要です。<c>ForceManager</c> が
    /// <see cref="RespawnFaction"/> を見て機動部隊かカオスかを決めます。
    /// 独自の派生システム (呼称や貢献度ルールが違う隊) に乗せたい波だけがここを名乗ります。
    ///
    /// 部隊システムに乗せたくない波は、<c>ForceBase</c> を返さずに
    /// <see cref="RespawnFaction"/> を <see cref="Faction.Unclassified"/> のままにしてください。
    /// </remarks>
    public virtual ForceBase CreateForce(byte? unitId, string unitName) => null;

    /// <summary>
    /// この SpawnSet の割り当て対象になりうるプレイヤーです。
    /// 既定では「まだ役職が割り当てられていない実プレイヤー」を返します。
    /// </summary>
    /// <remarks>
    /// ラウンド開始時はバニラの役職割り当てより前に走るため、その時点のプレイヤーは
    /// <see cref="RoleTypeId.Spectator"/> ではなく <see cref="RoleTypeId.None"/> (ロビー) です。
    /// ラウンド中に呼ばれた場合は Spectator が対象になります。
    /// Overwatch は <see cref="RoleTypeId.Overwatch"/> なのでここには含まれません。
    ///
    /// 既定は <c>IsSafePlayer</c> で実プレイヤーに絞ります。NPC も対象にしたい SpawnSet は
    /// ここを override して明示的に広げてください。
    /// </remarks>
    protected virtual List<Player> TargetPlayers()
    {
        // Assign は「先頭から取れば無作為」を前提にしているので、
        // 外から渡された一覧もここでシャッフルする。
        if (forcedCandidates is { } forced)
            return forced.Shuffled();

        return Player.List
            .Where(player => player.IsSafePlayer() &&
                             player.Role.Type is RoleTypeId.None or RoleTypeId.Spectator)
            .Shuffled();
    }

    /// <summary>
    /// 対象を指定して割り当てを実行します。リスポーンシステムから使います。
    /// </summary>
    /// <remarks>
    /// <see cref="RespawnRatio"/> はここでだけ効きます。
    /// 渡した一覧は呼び出しの間だけ使われ、終われば元の <see cref="TargetPlayers"/> に戻ります。
    /// </remarks>
    /// <returns>実際に役職を割り当てたプレイヤー。割り当てた順。</returns>
    public List<Player> SpawnFor(IReadOnlyList<Player> candidates)
    {
        if (candidates is null || candidates.Count == 0)
            return [];

        forcedCandidates = new List<Player>(candidates);

        try
        {
            return Spawn();
        }
        finally
        {
            forcedCandidates = null;
        }
    }

    /// <summary>
    /// 割り当てを実行します。
    /// </summary>
    /// <returns>実際に役職を割り当てたプレイヤー。割り当てた順。</returns>
    public List<Player> Spawn()
    {
        bool alreadyLocked = Round.IsLocked;

        if (!alreadyLocked)
            Round.IsLocked = true;

        // 途中で return してもラウンドロックを必ず戻す。
        try
        {
            OnSpawning();

            List<Player> spawned = SpawnInternal();

            OnSpawned();

            return spawned;
        }
        finally
        {
            if (!alreadyLocked)
                Round.IsLocked = false;
        }
    }

    /// <summary>
    /// 役職の割り当てを始める直前に呼ばれます。
    /// </summary>
    protected virtual void OnSpawning()
    {
    }

    /// <summary>
    /// 役職の割り当てが終わった直後に呼ばれます。割り当て人数が 0 人でも呼ばれます。
    /// </summary>
    protected virtual void OnSpawned()
    {
    }

    private List<Player> SpawnInternal()
    {
        if (AllowedPlayerCount < -1)
        {
            Log.Error($"[Slafight] SpawnSet '{Name}' の AllowedPlayerCount が不正です: {AllowedPlayerCount} (-1 以上にしてください)");

            return [];
        }

        if (SpawnRoles is null || SpawnRoles.Count == 0 || AllowedPlayerCount == 0)
            return [];

        List<Player> candidates = TargetPlayers();

        if (candidates.Count == 0)
            return [];

        int allowed = ResolveAllowedCount(candidates.Count);

        // TargetPlayers はシャッフル済みなので、先頭から必要人数を取るだけでランダム選出になる。
        if (allowed > 0 && candidates.Count > allowed)
            candidates = candidates.Take(allowed).ToList();

        List<SpawnRoleState> states = SpawnRoles
            .Shuffled()
            .Select(role => new SpawnRoleState(role))
            .ToList();

        List<Player> spawned = [];

        // 必須の行を先に埋めてから、残りを回す。
        AssignRoundRobin(states.Where(state => state.Definition.IsForced).ToList(), candidates, spawned);

        List<SpawnRoleState> rest = states.Where(state => !state.Definition.IsForced).ToList();

        if (RoundRobin)
            AssignRoundRobin(rest, candidates, spawned);
        else
            Assign(rest, candidates, spawned);

        return spawned;
    }

    /// <summary>
    /// 実際に何人まで配るかを決めます。
    /// 波として呼ばれたときだけ <see cref="RespawnRatio"/> が効きます。
    /// </summary>
    private int ResolveAllowedCount(int candidateCount)
    {
        if (forcedCandidates is null || RespawnRatio >= 1f)
            return AllowedPlayerCount;

        // 割合を掛けても 0 人にはしない。呼ばれた以上は最低 1 人出す。
        int byRatio = Mathf.Max(1, (int)(candidateCount * Mathf.Max(0f, RespawnRatio)));

        return AllowedPlayerCount < 0 ? byRatio : Mathf.Min(AllowedPlayerCount, byRatio);
    }

    /// <summary>
    /// 行ごとに 1 人ずつ順番に埋めます。
    /// </summary>
    /// <remarks>
    /// 重み抽選にしないのは、確率で漏れると「必ず埋める」にならないためです。
    /// 埋まった行は次の巡から外れます。
    /// </remarks>
    private void AssignRoundRobin(IReadOnlyList<SpawnRoleState> states, List<Player> candidates, List<Player> spawned)
    {
        if (states.Count == 0) return;

        bool assignedAny = true;

        // 1 巡で各行 1 人ずつ。埋まった行は次の巡から外れる。
        while (assignedAny && candidates.Count > 0)
        {
            assignedAny = false;

            foreach (SpawnRoleState state in states)
            {
                if (!state.IsAvailable || candidates.Count == 0) continue;

                if (!TryTake(state, candidates, spawned)) continue;

                assignedAny = true;
            }
        }
    }

    /// <summary>
    /// 候補を 1 人取って、この行に割り当てます。
    /// </summary>
    /// <returns>割り当てられたら true。行が壊れていれば false。</returns>
    private bool TryTake(SpawnRoleState state, List<Player> candidates, List<Player> spawned)
    {
        // TargetPlayers はシャッフル済みなので、先頭から取れば無作為に選んだのと同じ。
        Player target = candidates[0];
        candidates.RemoveAt(0);

        if (!TrySpawn(state, target))
        {
            // 割り当てに失敗した行は以後選ばない。無限ループを避ける。
            state.IsBroken = true;

            // この対象はまだ役職を持っていない。別の行が拾えるよう列の先頭へ戻す。
            candidates.Insert(0, target);

            return false;
        }

        // 役職側の SpawnPosition より SpawnSet 側の指定を優先する。
        if (OverridePosition is { } position)
            target.Position = position;

        state.AssignedCount++;
        spawned.Add(target);

        return true;
    }

    /// <summary>
    /// 割り当てた分を <paramref name="spawned"/> に積んでいきます。
    /// </summary>
    private void Assign(IReadOnlyList<SpawnRoleState> states, List<Player> candidates, List<Player> spawned)
    {
        if (states.Count == 0)
            return;

        while (candidates.Count > 0)
        {
            // 全行が壊れたら PickNext が null を返して抜けるので、回り続けない。
            if (PickNext(states) is not { } state)
                break;

            TryTake(state, candidates, spawned);
        }
    }

    /// <summary>
    /// 1 行ぶんの割り当てを行います。
    /// </summary>
    /// <remarks>
    /// <b>1 つの役職が落ちても割り当て全体を巻き添えにしません。</b>
    /// ここで例外を通すと <see cref="Spawn"/> ごと抜けてしまい、
    /// 「SCP だけ配られて人間側が誰も配られない」ような、
    /// 原因の見えない中断になります。落ちた行だけを諦めて先へ進みます。
    /// </remarks>
    private bool TrySpawn(SpawnRoleState state, Player target)
    {
        try
        {
            return state.Definition.SpawnFor(target);
        }
        catch (Exception exception)
        {
            Log.Error($"[Slafight] SpawnSet '{Name}' の役職割り当てで例外が発生しました: {exception}");

            return false;
        }
    }

    /// <summary>
    /// まだ枠が残っている行から 1 つ選びます。<see cref="SpawnSetRoleDefinition.Weight"/> で
    /// 出やすさに差を付けられます。全部同じ重みなら等確率です。
    /// </summary>
    private static SpawnRoleState PickNext(IReadOnlyList<SpawnRoleState> states)
    {
        float total = 0f;

        foreach (SpawnRoleState state in states)
        {
            if (state.IsAvailable)
                total += Mathf.Max(0f, state.Definition.Weight);
        }

        if (total <= 0f)
            return null;

        float roll = UnityEngine.Random.value * total;

        foreach (SpawnRoleState state in states)
        {
            if (!state.IsAvailable)
                continue;

            roll -= Mathf.Max(0f, state.Definition.Weight);

            if (roll <= 0f)
                return state;
        }

        // 浮動小数の誤差で漏れたときの保険。
        return states.LastOrDefault(state => state.IsAvailable);
    }

    private sealed class SpawnRoleState(SpawnSetRoleDefinition definition)
    {
        public SpawnSetRoleDefinition Definition { get; } = definition;

        public int AssignedCount { get; set; }

        public bool IsBroken { get; set; }

        public bool IsAvailable => !IsBroken && AssignedCount < Definition.Count;
    }
}
