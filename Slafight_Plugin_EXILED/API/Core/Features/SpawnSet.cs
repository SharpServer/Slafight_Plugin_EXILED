using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Enums;
using Slafight_Plugin_EXILED.API.Core.Extensions;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.ForceSystem;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// どの役職を何人ずつ割り当てるかを宣言します。波も SpawnSet の派生として定義します。
/// </summary>
public abstract class SpawnSet
{
    /// <summary>
    /// このラウンドで 1 人以上割り当てた SpawnSet です。出た順。
    /// </summary>
    private static readonly List<SpawnSet> History = [];

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
    /// 重み抽選ではなく行ごとに 1 人ずつ順番に配るかどうか。<see cref="SpawnSetRoleDefinition.Weight"/> は無視されます。
    /// </summary>
    public virtual bool RoundRobin => false;

    // ───────────────────────────────
    // リスポーンウェーブとして使うときの宣言
    //
    // 波を表す中間基底クラスも、波を指す enum も作りません。
    // 波は SpawnSet の派生クラスそのもので、下の virtual を override して名乗ります。
    // ラウンド開始時の一括割り当てのように波ではない SpawnSet は、何も override しません。
    // ───────────────────────────────

    /// <summary>
    /// この波が属する陣営です。自動抽選では現在の Context の重みと併せて判定します。
    /// </summary>
    public virtual Faction RespawnFaction => Faction.Unclassified;

    /// <summary>
    /// 本隊ではなく増援 (ミニウェーブ) かどうか。
    /// </summary>
    public virtual bool IsMiniWave => false;

    /// <summary>
    /// リスポーン抽選での重みです。0 なら自動抽選に出ません。
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
    /// 1 人以上割り当てたときに再生する車両演出です。既定は車両なし。
    /// </summary>
    public virtual SpawnVehicle Vehicle => SpawnVehicle.None;

    /// <summary>
    /// 出撃放送を組み立てます。何も積まなければ無音です。
    /// 季節ごとの差し替えは <see cref="SpawnAnnouncement.Seasonal{T}"/> で書きます。残存 SCP 数は後ろに自動で付きます。
    /// </summary>
    protected virtual void BuildAnnouncement(SpawnAnnouncement announcement)
    {
    }

    /// <summary>
    /// 出撃アナウンスの最後に、バニラ同様の残存 SCP 数を付けるか。
    /// 既定は機動部隊 (FoundationStaff) の波すべてです。
    /// </summary>
    public virtual bool AnnouncesRemainingScps => RespawnFaction is Faction.FoundationStaff;

    /// <summary>
    /// 残存 SCP 数の放送です。<see cref="AnnouncesRemainingScps"/> のとき <see cref="BuildAnnouncement"/> の後に呼ばれます。
    /// </summary>
    protected virtual void BuildRemainingScps(SpawnAnnouncement announcement)
    {
        int count = announcement.RemainingScpCount;

        string subtitle = count == 0
            ? announcement.Seasonal("施設内に<color=#008F1E>危険な勢力</color>が存在します。警戒してください。")
                .On(SeasonTypeId.Halloween, "施設内に潜在的な<color=#008F1E>脅威</color>が存在します。警戒してください。")
            : announcement.Seasonal($"現在、収容違反中のSCPは{count}体です。")
                .On(SeasonTypeId.Halloween, $"{count}体の幽霊は再収容が実施されていません。");

        // バニラのクリスマス分岐と同じく、0 体でも数をそのまま読み上げる。
        if (announcement.Is(SeasonTypeId.Christmas))
        {
            announcement.Say($"{count} XMAS_SCPSUBJECTS", subtitle);

            return;
        }

        string subjects = announcement.Seasonal(count == 1 ? "ScpSubject" : "ScpSubjects")
            .On(SeasonTypeId.Halloween, "HALLOWEEN_SPECTER");

        announcement.Say(count == 0 ? "NoScpsLeft" : $"AwaitingRecontainment {count} {subjects}", subtitle);
    }

    /// <summary>
    /// 出撃放送を完成させます。<see cref="BuildAnnouncement"/> → <see cref="BuildRemainingScps"/> の順に積みます。
    /// </summary>
    /// <param name="unitName">部隊名 (<c>ALPHA-01</c> 形式)。無ければ null。</param>
    /// <param name="players">出たプレイヤー。プレビューなら null。</param>
    public SpawnAnnouncement ComposeAnnouncement(int spawnCount, string unitName, IReadOnlyList<Player> players = null)
    {
        SpawnAnnouncement announcement = new SpawnAnnouncement(this, spawnCount, players, unitName);

        try
        {
            BuildAnnouncement(announcement);

            if (!announcement.IsEmpty && AnnouncesRemainingScps)
                BuildRemainingScps(announcement);
        }
        catch (Exception exception)
        {
            Log.Error($"[Slafight] SpawnSet '{Name}' の出撃放送の組み立てで例外が発生しました: {exception}");
            announcement.Clear();
        }

        return announcement;
    }

    /// <summary>
    /// この波が作る隊です。null なら <see cref="RespawnFaction"/> から自動で決まります。
    /// </summary>
    public virtual ForceBase CreateForce(byte? unitId, string unitName) => null;

    /// <summary>
    /// 1 ラウンドに自動抽選で選ばれる回数の上限です。-1 なら無制限。
    /// </summary>
    public virtual int TriggerLimit => -1;

    /// <summary>
    /// このラウンドで出た回数です。1 人以上割り当てた回だけ数えます。
    /// </summary>
    public int TriggerCount => History.Count(set => set.GetType() == GetType());

    /// <summary>
    /// いま自動抽選に出せるか。<see cref="TriggerLimit"/> と <see cref="TriggerCondition"/> を見ます。
    /// 手動実行と <see cref="SpawnOverride"/> では見ません。
    /// </summary>
    public bool CanTrigger
    {
        get
        {
            if (TriggerLimit >= 0 && TriggerCount >= TriggerLimit)
                return false;

            try
            {
                return TriggerCondition();
            }
            catch (Exception exception)
            {
                Log.Error($"[Slafight] SpawnSet '{Name}' の TriggerCondition で例外が発生しました: {exception}");

                return false;
            }
        }
    }

    /// <summary>
    /// このラウンドで 1 人以上割り当てた SpawnSet です。出た順。
    /// </summary>
    public static IReadOnlyList<SpawnSet> SpawnedThisRound => History;

    /// <summary>
    /// このラウンドでこの陣営の波が出た回数です。
    /// </summary>
    /// <param name="miniWave">本隊 / 増援で絞ります。null ならどちらも数えます。</param>
    public static int WaveCount(Faction faction, bool? miniWave = null) =>
        History.Count(set => set.RespawnFaction == faction && (miniWave is null || set.IsMiniWave == miniWave));

    /// <summary>
    /// 自動抽選に出す追加条件です。例: <c>WaveCount(Faction.FoundationStaff) == 0</c> で初回の波だけ。
    /// </summary>
    protected virtual bool TriggerCondition() => true;

    /// <summary>
    /// 割り当て対象になりうるプレイヤーです。既定は役職未割り当ての実プレイヤー (NPC を除く) です。
    /// </summary>
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
    /// 対象を指定して割り当てを実行します。<see cref="RespawnRatio"/> はここでだけ効きます。
    /// </summary>
    /// <returns>割り当てたプレイヤー (割り当て順)。</returns>
    public List<Player> SpawnFor(IReadOnlyList<Player> candidates)
    {
        if (candidates is null || candidates.Count == 0)
            return [];

        forcedCandidates = [.. candidates];

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

            if (spawned.Count > 0)
            {
                History.Add(this);
                PlayVehicle();
            }

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
    /// 履歴を消します。ラウンドの切り替わりで呼びます。
    /// </summary>
    internal static void ClearHistory() => History.Clear();

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

    private void PlayVehicle()
    {
        // 演出の失敗で割り当て済みプレイヤーや Spawned 通知を失わないようにする。
        try
        {
            switch (Vehicle)
            {
                case SpawnVehicle.None:
                    break;
                case SpawnVehicle.NtfChopper:
                    Respawn.SummonNtfChopper();
                    break;
                case SpawnVehicle.ChaosVan:
                    Respawn.SummonChaosInsurgencyVan();
                    break;
                default:
                    Log.Warn($"[Slafight] SpawnSet '{Name}' の車両指定が不正です: {Vehicle}");
                    break;
            }
        }
        catch (Exception exception)
        {
            Log.Error($"[Slafight] SpawnSet '{Name}' の車両演出で例外が発生しました: {exception}");
        }
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
            candidates = [.. candidates.Take(allowed)];

        List<SpawnRoleState> states =
        [
            .. SpawnRoles
                .Shuffled()
                .Select(role => new SpawnRoleState(role))
        ];

        List<Player> spawned = [];

        // 必須の行を先に埋めてから、残りを回す。
        AssignRoundRobin([.. states.Where(state => state.Definition.IsForced)], candidates, spawned);

        List<SpawnRoleState> rest = [.. states.Where(state => !state.Definition.IsForced)];

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
                if (candidates.Count == 0 || !CanTake(state, candidates, spawned)) continue;

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
            if (PickNext(states, candidates, spawned) is not { } state)
                break;

            TryTake(state, candidates, spawned);
        }
    }

    /// <summary>
    /// 1 行ぶんを割り当てます。例外は握りつぶし、その行だけを諦めます。
    /// </summary>
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
    /// 枠が残っていて、条件 (<see cref="SpawnSetRoleDefinition.When"/>) も満たす行か。
    /// </summary>
    private bool CanTake(SpawnRoleState state, List<Player> candidates, List<Player> spawned)
    {
        if (!state.IsAvailable)
            return false;

        try
        {
            return state.Definition.Allows(
                new SpawnRoleContext(this, spawned.Count, spawned.Count + candidates.Count, state.AssignedCount));
        }
        catch (Exception exception)
        {
            Log.Error($"[Slafight] SpawnSet '{Name}' の役職条件で例外が発生しました: {exception}");

            // 条件が壊れた行は以後選ばない。
            state.IsBroken = true;

            return false;
        }
    }

    /// <summary>
    /// 選べる行から 1 つ選びます。<see cref="SpawnSetRoleDefinition.Weight"/> で
    /// 出やすさに差を付けられます。全部同じ重みなら等確率です。
    /// </summary>
    private SpawnRoleState PickNext(IReadOnlyList<SpawnRoleState> states, List<Player> candidates, List<Player> spawned)
    {
        // 条件は 1 回の抽選につき 1 度だけ評価する。
        List<SpawnRoleState> eligible = [.. states.Where(state => CanTake(state, candidates, spawned))];

        float total = eligible.Sum(state => Mathf.Max(0f, state.Definition.Weight));

        if (total <= 0f)
            return null;

        float roll = UnityEngine.Random.value * total;

        foreach (SpawnRoleState state in eligible)
        {
            roll -= Mathf.Max(0f, state.Definition.Weight);

            if (roll <= 0f)
                return state;
        }

        // 浮動小数の誤差で漏れたときの保険。
        return eligible.LastOrDefault(state => state.Definition.Weight > 0f);
    }

    private sealed class SpawnRoleState(SpawnSetRoleDefinition definition)
    {
        public SpawnSetRoleDefinition Definition { get; } = definition;

        public int AssignedCount { get; set; }

        public bool IsBroken { get; set; }

        public bool IsAvailable => !IsBroken && AssignedCount < Definition.Count;
    }
}
