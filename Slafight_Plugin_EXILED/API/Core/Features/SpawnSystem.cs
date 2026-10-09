using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Server;
using PlayerRoles;
using Respawning.NamingRules;
using Slafight_Plugin_EXILED.Extensions;

using ServerHandlers = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// リスポーンウェーブを引き受けます。バニラのウェーブは常に取り消し、
/// どの <see cref="SpawnSet"/> を出すかをこちらで決めます。
/// </summary>
/// <remarks>
/// 旧実装は <c>SpawnTypeId</c> という 24 個の enum と、
/// <c>Dictionary&lt;SpawnTypeId, Dictionary&lt;SpawnRoleKey, (float, bool)&gt;&gt;</c> という
/// 引き表で波を表していました。ここではその両方を持ちません。
/// <b>波は <see cref="SpawnSet"/> の派生クラスそのものです。</b>
///
/// このクラスはどこからも登録されていません。<see cref="EventHandlerBase"/> を
/// 継承しているだけで <see cref="EventHandlerRegistry"/> が購読させます。
/// </remarks>
public sealed class SpawnSystem : EventHandlerBase
{
    private static readonly List<RegisteredOverride> Overrides = [];

    private static long sequence;

    private static bool spawning;

    /// <summary>登録済みの SpawnSystem が車両演出を引き受けているか。</summary>
    internal static bool ControlsVehicleEffects { get; private set; }

    /// <summary>
    /// 割り当て中の波に配る部隊番号です。<see cref="Summon"/> の間だけ入ります。
    /// </summary>
    private static byte? pendingUnitId;

    /// <summary>
    /// 直近にこちらが配った部隊番号です。まだ 1 度も配っていなければ -1。
    /// </summary>
    private static int lastIssuedUnitId = -1;

    /// <summary>
    /// 波を出す直前に呼ばれます。<see cref="SpawningEventArgs.Wave"/> を差し替えれば別の波になり、
    /// <see cref="SpawningEventArgs.IsAllowed"/> を false にすれば出ません。
    /// </summary>
    public static event Action<SpawningEventArgs> Spawning;

    /// <summary>
    /// 波を出した直後に呼ばれます。演出はここで拾ってください。
    /// </summary>
    public static event Action<SpawnedEventArgs> Spawned;

    /// <summary>
    /// 一時的にリスポーンを止めます。
    /// </summary>
    /// <remarks>
    /// 基底の <see cref="EventHandlerBase.Disable"/> と紛らわしい名前にしないこと。
    /// 移植元では <c>public static new bool Disable</c> が基底のメソッドを隠していました。
    /// </remarks>
    public static bool IsSuspended { get; set; }

    /// <summary>
    /// 抽選に使う乱数です。試験で固定したいときに差し替えます。
    /// </summary>
    public static Func<int, int> IndexSelector { get; set; } = max => UnityEngine.Random.Range(0, max);

    /// <inheritdoc />
    public override void RegisterEvents()
    {
        ServerHandlers.RespawningTeam += OnRespawningTeam;
        ServerHandlers.RestartingRound += ResetRuntimeState;
        ServerHandlers.WaitingForPlayers += ResetRuntimeState;
        ControlsVehicleEffects = true;
    }

    /// <inheritdoc />
    public override void UnregisterEvents()
    {
        ControlsVehicleEffects = false;
        ServerHandlers.RespawningTeam -= OnRespawningTeam;
        ServerHandlers.RestartingRound -= ResetRuntimeState;
        ServerHandlers.WaitingForPlayers -= ResetRuntimeState;
        ResetRuntimeState();
    }

    /// <summary>
    /// 次に条件が合ったウェーブを、指定した波に差し替えます。
    /// </summary>
    /// <returns>取り消しに使う手札。</returns>
    public static Guid AddOverride(SpawnOverride rule)
    {
        if (rule?.Wave is null)
            throw new ArgumentNullException(nameof(rule));

        Guid id = Guid.NewGuid();
        Overrides.Add(new RegisteredOverride(id, rule, ++sequence));

        return id;
    }

    /// <summary>
    /// 予約した差し替えを取り消します。
    /// </summary>
    public static bool RemoveOverride(Guid id) => Overrides.RemoveAll(entry => entry.Id == id) > 0;

    /// <summary>
    /// 予約した差し替えをすべて取り消します。
    /// </summary>
    public static void ClearOverrides() => Overrides.Clear();

    /// <summary>
    /// バニラのタイマーを待たず、いますぐ波を出します。
    /// </summary>
    /// <returns>実際に出たプレイヤー。割り当てた順。</returns>
    public static List<Player> ForceSpawn(SpawnSet wave) => Summon(wave, null);

    /// <summary>
    /// この波の対象になりうるプレイヤーです。
    /// </summary>
    /// <remarks>
    /// 既にカスタム役職を持っている観戦者は対象外です。
    /// 二重に配ると前の役職の後始末が走り、per-player 状態が失われます。
    /// </remarks>
    private static List<Player> Candidates()
    {
        return Player.List
            .Where(player => player.IsSafePlayer() &&
                             player.Role.Type is RoleTypeId.Spectator &&
                             CustomRole.Of(player) is null)
            .ToList();
    }

    private static void OnRespawningTeam(RespawningTeamEventArgs ev)
    {
        // バニラのウェーブは常に取り消す。誰を出すかはこちらが決める。
        ev.IsAllowed = false;

        if (IsSuspended || spawning) return;

        Faction faction = ev.NextKnownTeam;
        bool miniWave = ev.Wave?.IsMiniWave ?? false;

        if (TakeOverride(ev, faction, miniWave) is { } forced)
        {
            Summon(forced, ev);

            return;
        }

        if (faction is not (Faction.FoundationStaff or Faction.FoundationEnemy)) return;

        SpawnContext context = SpawnContext.Active;
        SpawnSet selected = context.Roll(faction, miniWave, IndexSelector);

        if (selected is null) return;

        Summon(selected, ev);
    }

    /// <summary>
    /// 条件に合う差し替えを 1 つ取り出します。優先度が高いものを、同じなら先に予約したものを選びます。
    /// </summary>
    private static SpawnSet TakeOverride(RespawningTeamEventArgs ev, Faction faction, bool miniWave)
    {
        RegisteredOverride match = Overrides
            .Where(entry => entry.Rule.Matches(ev, faction, miniWave))
            .OrderByDescending(entry => entry.Rule.Priority)
            .ThenBy(entry => entry.Sequence)
            .FirstOrDefault();

        if (match is null) return null;

        Overrides.Remove(match);

        return match.Rule.Wave;
    }

    /// <summary>
    /// 波を実際に出します。
    /// </summary>
    private static List<Player> Summon(SpawnSet wave, RespawningTeamEventArgs source)
    {
        if (wave is null) return [];

        List<Player> candidates = Candidates();

        if (candidates.Count == 0) return [];

        SpawningEventArgs spawningArgs = new SpawningEventArgs(wave, SpawnContext.Active, candidates, source, PeekUnitId(wave));
        Spawning?.Invoke(spawningArgs);

        if (!spawningArgs.IsAllowed || spawningArgs.Wave is null) return [];

        // 波が差し替えられていることがあるので、実際に出す波で採番する。
        (byte? unitId, string unitName) = CommitUnit(spawningArgs.Wave);

        spawning = true;
        pendingUnitId = unitId;

        if (unitId is not null)
            PlayerRoleManager.OnRoleChanged += ApplyUnitId;

        List<Player> spawned;

        try
        {
            spawned = spawningArgs.Wave.SpawnFor(candidates);
        }
        catch (Exception exception)
        {
            Log.Error($"[Slafight] ウェーブ '{wave.Name}' の割り当てで例外が発生しました: {exception}");

            return [];
        }
        finally
        {
            if (unitId is not null)
                PlayerRoleManager.OnRoleChanged -= ApplyUnitId;

            pendingUnitId = null;
            spawning = false;
        }

        if (spawned.Count == 0) return spawned;

        Spawned?.Invoke(new SpawnedEventArgs(spawningArgs.Wave, spawned, unitId, unitName));

        return spawned;
    }

    /// <summary>
    /// この波に配る部隊番号を決めます。名前はまだ作りません。
    /// FoundationStaff の波でなければ null。
    /// </summary>
    /// <remarks>
    /// <see cref="NamingRulesManager.GeneratedNames"/> はホストクライアントが
    /// <c>UnitNameMessage</c> を処理してから増えます。つまり
    /// <see cref="NamingRulesManager.ServerGenerateName"/> を呼んだ直後は、
    /// いま作った名前がまだ載っていません。反映待ちの分を踏み越えて
    /// 同じ番号を 2 度配らないよう、こちらが配った番号も覚えておいて大きい方を採ります。
    /// </remarks>
    private static byte? PeekUnitId(SpawnSet wave)
    {
        if (wave?.RespawnFaction is not Faction.FoundationStaff) return null;

        return NextUnitId();
    }

    /// <summary>
    /// 次に配れる部隊番号です。まだ確定させません。使える番号が無ければ null。
    /// </summary>
    /// <remarks>
    /// <b>部隊システムの命名もここを基点にします。</b>分隊名や非 NTF 部隊の名前を
    /// 別に採番すると、バニラが既に表示している部隊名と衝突します。
    /// 採番の基点を 2 箇所に持たせないため、<c>ForceNaming</c> からもこれを呼びます。
    ///
    /// <see cref="NamingRulesManager.GeneratedNames"/> はホストクライアントが
    /// <c>UnitNameMessage</c> を処理してから増えます。つまり
    /// <see cref="NamingRulesManager.ServerGenerateName"/> を呼んだ直後は、
    /// いま作った名前がまだ載っていません。反映待ちの分を踏み越えて
    /// 同じ番号を 2 度配らないよう、こちらが配った番号も覚えておいて大きい方を採ります。
    /// </remarks>
    internal static byte? NextUnitId()
    {
        if (!NamingRulesManager.TryGetNamingRule(Team.FoundationForces, out _)) return null;

        int generated = NamingRulesManager.GeneratedNames.TryGetValue(Team.FoundationForces, out List<string> names)
            ? names.Count
            : 0;

        int next = lastIssuedUnitId < 0 ? generated : Math.Max(generated, lastIssuedUnitId + 1);

        // UnitNameId は byte 1 つ。溢れるならこちらでは触らず、バニラの採番に任せる。
        return next > byte.MaxValue ? null : (byte)next;
    }

    /// <summary>
    /// 次の部隊番号を確定し、その番号の部隊名をバニラに作らせます。使える番号が無ければ null。
    /// </summary>
    /// <remarks>
    /// 波の採番と部隊システムの採番はここを共有します。
    /// 名前を作った時点で <see cref="lastIssuedUnitId"/> を進めるので、
    /// <see cref="NamingRulesManager.GeneratedNames"/> の反映を待たずに次を採れます。
    /// </remarks>
    internal static (byte? Id, string Name) IssueUnit()
    {
        if (NextUnitId() is not { } unitId) return (null, null);

        if (!NamingRulesManager.TryGetNamingRule(Team.FoundationForces, out UnitNamingRule rule)) return (null, null);

        NamingRulesManager.ServerGenerateName(Team.FoundationForces, rule);
        lastIssuedUnitId = unitId;

        // GeneratedNames はホストクライアントの受信待ちで遅れるが、
        // LastGeneratedName は GenerateNew が同期で入れるのでここで確実に読める。
        return (unitId, rule.LastGeneratedName);
    }

    /// <summary>
    /// この波に配る部隊番号と部隊名を確定します。FoundationStaff の波でなければどちらも null。
    /// </summary>
    private static (byte? Id, string Name) CommitUnit(SpawnSet wave)
    {
        if (wave?.RespawnFaction is not Faction.FoundationStaff) return (null, null);

        return IssueUnit();
    }

    /// <summary>
    /// 割り当て中のプレイヤーに、この波の部隊番号を書き込みます。
    /// </summary>
    /// <remarks>
    /// バニラの <c>HumanRole.Init</c> は <c>GeneratedNames.Count - 1</c>
    /// (理由が Respawn/RespawnMiniwave なら <c>Count</c>) を勝手に入れます。
    /// こちらは <c>Role.Set</c> 経由の ForceClass なので、放っておくと 1 つ前の部隊名になります。
    ///
    /// <see cref="PlayerRoleManager.OnRoleChanged"/> は <c>InitializeNewRole</c> の中、
    /// <c>SendNewRoleInfo</c> の手前で走ります。ここで書き換えれば
    /// <b>最初の RoleSyncInfo に載る</b>ので、同期を 2 度送らずに済みます。
    /// </remarks>
    private static void ApplyUnitId(ReferenceHub hub, PlayerRoleBase previous, PlayerRoleBase current)
    {
        if (pendingUnitId is not { } unitId) return;

        if (current is HumanRole { UsesUnitNames: true } humanRole)
            humanRole.UnitNameId = unitId;
    }

    private static void ResetRuntimeState()
    {
        Overrides.Clear();
        IsSuspended = false;
        spawning = false;
        pendingUnitId = null;
        lastIssuedUnitId = -1;
        SpawnContext.Reset();
    }

    private sealed class RegisteredOverride(Guid id, SpawnOverride rule, long sequence)
    {
        public Guid Id { get; } = id;

        public SpawnOverride Rule { get; } = rule;

        public long Sequence { get; } = sequence;
    }
}
