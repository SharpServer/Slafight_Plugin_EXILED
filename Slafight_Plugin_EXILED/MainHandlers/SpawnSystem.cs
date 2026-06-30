using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Server;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using Random = UnityEngine.Random;
using Slafight_Plugin_EXILED.API.Interface;

namespace Slafight_Plugin_EXILED.MainHandlers;

public class SpawnSystem : IBootstrapHandler, IDisposable
{
    public static void Register()
    {
        Unregister();
        UnitPackBootstrap.RegisterAllPacks();
        SpawnContextBootstrap.RegisterAllContexts(Config);
        _ = new SpawnSystem();
    }
    public static void Unregister()
    {
        Instance?.Dispose();
        Instance = null;
        UnitPackBootstrap.UnregisterAllPacks();
        SpawnContextBootstrap.UnregisterAllContexts();
        Spawning = null;
        Spawned = null;
        Disable = false;
        ResetOverride();
    }

    // =====================
    //  種別
    // =====================

    public enum SpawnRoleKind
    {
        Vanilla,
        Custom,
    }

    public readonly record struct SpawnRoleKey
    {
        public SpawnRoleKind Kind { get; }
        public RoleTypeId Vanilla { get; }
        public CRoleTypeId Custom { get; }

        public SpawnRoleKey(RoleTypeId vanilla)
        {
            Kind = SpawnRoleKind.Vanilla;
            Vanilla = vanilla;
            Custom = CRoleTypeId.None;
        }

        public SpawnRoleKey(CRoleTypeId custom)
        {
            Kind = SpawnRoleKind.Custom;
            Custom = custom;
            Vanilla = RoleTypeId.None;
        }
    }

    public enum SpawnOverrideMode
    {
        None,       // 通常
        NextWave,   // 次のRespawningTeamを上書き
        Immediate,  // 即時Summon
    }

    // =====================
    //  Config
    // =====================

    public class SpawnConfig
    {
        public Dictionary<SpawnTypeId, int> FoundationStaffWaveWeights { get; set; } = new()
        {
            { SpawnTypeId.MtfNtfNormal, 80 },
            { SpawnTypeId.MtfHdNormal,  20 },
            { SpawnTypeId.MtfSneNormal, 0 },
        };

        public Dictionary<SpawnTypeId, int> FoundationEnemyWaveWeights { get; set; } = new()
        {
            { SpawnTypeId.GoiChaosNormal,    100 },
            { SpawnTypeId.GoiFifthistNormal, 0   },
        };

        public Dictionary<SpawnTypeId, int> FoundationStaffMiniWaveWeights { get; set; } = new()
        {
            { SpawnTypeId.MtfNtfBackup, 80 },
            { SpawnTypeId.MtfHdBackup,  20 },
            { SpawnTypeId.MtfSneBackup, 0 },
        };

        public Dictionary<SpawnTypeId, int> FoundationEnemyMiniWaveWeights { get; set; } = new()
        {
            { SpawnTypeId.GoiChaosBackup,    100 },
            { SpawnTypeId.GoiFifthistBackup, 0   },
        };

        public Dictionary<SpawnTypeId, float> SpawnRatios { get; set; } = new()
        {
            { SpawnTypeId.MtfNtfNormal,      1.0f },
            { SpawnTypeId.MtfNtfBackup,      1.0f },
            { SpawnTypeId.MtfHdNormal,       1.0f },
            { SpawnTypeId.MtfHdBackup,       0.5f },
            { SpawnTypeId.GoiChaosNormal,    1.0f },
            { SpawnTypeId.GoiChaosBackup,    1.0f },
            { SpawnTypeId.GoiFifthistNormal, 1.0f },
            { SpawnTypeId.GoiFifthistBackup, 0.5f },
        };

        public int ScpThresholdHigh    { get; set; } = 3;
        public int PlayerThresholdHigh { get; set; } = 6;

        public int S3005FifthistChance { get; set; } = 5;
        public bool NatoCallsign       { get; set; } = true;
    }

    public static SpawnConfig Config { get; } = new();

    // =====================
    //  CustomSpawningEventArgs
    // =====================

    public class CustomSpawningEventArgs : EventArgs
    {
        /// <summary>この Wave のスポーンを許可するかどうか。</summary>
        public bool IsAllowed { get; set; } = true;

        /// <summary>現在有効なコンテキスト。</summary>
        public SpawnContext NowContext { get; }

        /// <summary>
        /// この Wave で使用する weights。
        /// デフォルトでは NowContext からコピーされた値で初期化される。
        /// </summary>
        public Dictionary<SpawnTypeId, int> ContextOverride { get; }

        /// <summary>
        /// スポーンさせる SpawnTypeId。
        /// null の場合は ContextOverride から抽選される。
        /// </summary>
        public SpawnTypeId? SpawnType { get; set; }

        /// <summary>今回の Wave の陣営。</summary>
        public Faction Faction { get; }

        /// <summary>MiniWave かどうか。</summary>
        public bool IsMiniWave { get; }

        /// <summary>元の RespawningTeamEventArgs。</summary>
        public RespawningTeamEventArgs SourceEventArgs { get; }

        /// <summary>実際にスポーンさせた人数（スポーン前は 0）。</summary>
        public int SpawnCount { get; set; }

        /// <summary>Cassie 用コールサイン（NATO_A など）。</summary>
        public string CassieCallsign { get; set; } = string.Empty;

        /// <summary>表示用コールサイン（ALPHA-05 など）。</summary>
        public string DisplayCallsign { get; set; } = string.Empty;

        public CustomSpawningEventArgs(
            RespawningTeamEventArgs sourceEventArgs,
            SpawnContext nowContext,
            Dictionary<SpawnTypeId, int> baseWeights,
            Faction faction,
            bool isMiniWave)
        {
            SourceEventArgs = sourceEventArgs;
            NowContext = nowContext;
            Faction = faction;
            IsMiniWave = isMiniWave;
            ContextOverride = new Dictionary<SpawnTypeId, int>(baseWeights);
        }
    }

    /// <summary>
    /// スポーン決定前に呼ばれるイベント。
    /// ContextOverride / SpawnType / IsAllowed を通じて今回の Wave を自由にカスタマイズできる。
    /// </summary>
    public static event EventHandler<CustomSpawningEventArgs> Spawning;

    /// <summary>
    /// スポーン処理完了後に呼ばれるイベント。
    /// 引数は CustomSpawningEventArgs を使い回し、SpawnType / SpawnCount / Callsign などが埋まった状態になる。
    /// </summary>
    public static event EventHandler<CustomSpawningEventArgs> Spawned;

    // =====================
    //  状態フラグ
    // =====================

    private bool _defaultWaveGateOpen = true;
    private CoroutineHandle _defaultWaveResetHandle;
    public static bool Disable = false;

    private static readonly List<RegisteredNextWaveOverride> PendingOverrides = new();
    private static long _overrideSequence;

    public static SpawnOverrideMode OverrideMode => PendingOverrides.Count > 0
        ? SpawnOverrideMode.NextWave
        : SpawnOverrideMode.None;

    public static SpawnTypeId? PendingOverrideType => PendingOverrides.FirstOrDefault()?.Rule.SpawnType;
    public static bool PendingMiniWave => PendingOverrides.FirstOrDefault()?.Rule.OverrideMiniWave ?? false;
    public static int PendingOverrideCount => PendingOverrides.Count;

    public static SpawnSystem Instance { get; private set; }
    private bool _disposed;

    /// <summary>
    /// 次に発生する Wave を置換するための条件付きルール。
    /// 指定した条件はすべて AND 条件として評価される。
    /// </summary>
    public sealed class NextSpawnOverride
    {
        public NextSpawnOverride(SpawnTypeId spawnType)
        {
            SpawnType = spawnType;
        }

        public SpawnTypeId SpawnType { get; }
        public SpawnableFaction? SourceSpawnableFaction { get; set; }
        public Faction? SourceFaction { get; set; }
        public bool? SourceIsMiniWave { get; set; }
        public bool? OverrideMiniWave { get; set; }
        public Func<RespawningTeamEventArgs, bool> Predicate { get; set; }
        public int Priority { get; set; }

        internal bool Matches(RespawningTeamEventArgs ev)
        {
            if (SourceSpawnableFaction.HasValue &&
                ev.Wave.SpawnableFaction != SourceSpawnableFaction.Value)
                return false;

            if (SourceFaction.HasValue && ev.NextKnownTeam != SourceFaction.Value)
                return false;

            if (SourceIsMiniWave.HasValue && ev.Wave.IsMiniWave != SourceIsMiniWave.Value)
                return false;

            return Predicate == null || Predicate(ev);
        }

        internal NextSpawnOverride Snapshot()
        {
            return new NextSpawnOverride(SpawnType)
            {
                SourceSpawnableFaction = SourceSpawnableFaction,
                SourceFaction = SourceFaction,
                SourceIsMiniWave = SourceIsMiniWave,
                OverrideMiniWave = OverrideMiniWave,
                Predicate = Predicate,
                Priority = Priority,
            };
        }
    }

    private sealed class RegisteredNextWaveOverride
    {
        public RegisteredNextWaveOverride(
            Guid registrationId,
            NextSpawnOverride rule,
            string source,
            long sequence)
        {
            RegistrationId = registrationId;
            Rule = rule;
            Source = source;
            Sequence = sequence;
        }

        public Guid RegistrationId { get; }
        public NextSpawnOverride Rule { get; }
        public string Source { get; }
        public long Sequence { get; }
    }

    // =====================
    //  コンストラクタ
    // =====================

    public SpawnSystem()
    {
        Instance = this;
        Exiled.Events.Handlers.Server.RespawningTeam += SpawnHandler;
        Exiled.Events.Handlers.Server.RestartingRound += OnRoundRestarting;
        Exiled.Events.Handlers.Server.WaitingForPlayers += OnWaitingForPlayers;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Exiled.Events.Handlers.Server.RespawningTeam -= SpawnHandler;
        Exiled.Events.Handlers.Server.RestartingRound -= OnRoundRestarting;
        Exiled.Events.Handlers.Server.WaitingForPlayers -= OnWaitingForPlayers;

        if (_defaultWaveResetHandle.IsRunning)
            Timing.KillCoroutines(_defaultWaveResetHandle);

        GC.SuppressFinalize(this);
    }

    // =====================
    //  外部からの切り替えAPI
    // =====================

    public static void ReplaceNextSpawn(SpawnTypeId spawnType, bool? isMiniWave = null, string source = null)
    {
        ClearNextSpawnOverrides("replaced");
        AddNextSpawnOverride(new NextSpawnOverride(spawnType)
        {
            OverrideMiniWave = isMiniWave,
        }, source);
    }

    public static Guid AddNextSpawnOverride(NextSpawnOverride rule, string source = null)
    {
        if (rule == null)
            throw new ArgumentNullException(nameof(rule));

        return AddNextSpawnOverrides(new[] { rule }, source);
    }

    public static Guid AddNextSpawnOverride(
        SpawnableFaction sourceFaction,
        SpawnTypeId spawnType,
        bool? overrideMiniWave = null,
        string source = null,
        int priority = 0)
    {
        return AddNextSpawnOverride(new NextSpawnOverride(spawnType)
        {
            SourceSpawnableFaction = sourceFaction,
            OverrideMiniWave = overrideMiniWave,
            Priority = priority,
        }, source);
    }

    public static Guid AddNextSpawnOverride(
        Faction sourceFaction,
        SpawnTypeId spawnType,
        bool? sourceIsMiniWave = null,
        bool? overrideMiniWave = null,
        string source = null,
        int priority = 0)
    {
        return AddNextSpawnOverride(new NextSpawnOverride(spawnType)
        {
            SourceFaction = sourceFaction,
            SourceIsMiniWave = sourceIsMiniWave,
            OverrideMiniWave = overrideMiniWave,
            Priority = priority,
        }, source);
    }

    /// <summary>
    /// 複数の候補を1つの one-shot グループとして登録する。
    /// いずれか1ルールが一致すると、同じグループの全ルールが消費される。
    /// </summary>
    public static Guid AddNextSpawnOverrides(
        IEnumerable<NextSpawnOverride> rules,
        string source = null)
    {
        if (rules == null)
            throw new ArgumentNullException(nameof(rules));

        var ruleList = rules.ToList();
        if (ruleList.Count == 0)
            throw new ArgumentException("At least one override rule is required.", nameof(rules));
        if (ruleList.Any(rule => rule == null))
            throw new ArgumentException("Override rules cannot contain null.", nameof(rules));

        var registrationId = Guid.NewGuid();
        var normalizedSource = string.IsNullOrWhiteSpace(source) ? "external" : source;

        foreach (var rule in ruleList)
        {
            PendingOverrides.Add(new RegisteredNextWaveOverride(
                registrationId,
                rule.Snapshot(),
                normalizedSource,
                _overrideSequence++));
        }

        Log.Info(
            $"SpawnSystem: Queued next-wave override group {registrationId} with {ruleList.Count} rule(s). Source={normalizedSource}");
        return registrationId;
    }

    public static bool RemoveNextSpawnOverride(Guid registrationId, string reason = "manual")
    {
        int removed = PendingOverrides.RemoveAll(entry => entry.RegistrationId == registrationId);
        if (removed > 0)
        {
            Log.Debug(
                $"SpawnSystem: Removed next-wave override group {registrationId} ({removed} rule(s)) because {reason}.");
        }

        return removed > 0;
    }

    public static void ClearNextSpawnOverrides(string reason = "manual")
    {
        if (PendingOverrides.Count > 0)
        {
            Log.Debug(
                $"SpawnSystem: Cleared {PendingOverrides.Count} pending next-wave override rule(s) because {reason}.");
        }

        PendingOverrides.Clear();
        _overrideSequence = 0;
    }

    public static void ForceSpawnNow(SpawnTypeId spawnType, bool isMiniWave = false)
    {
        ResetOverride();
        Log.Info($"SpawnSystem: ForceSpawnNow {spawnType} (Mini:{isMiniWave})");
        Instance?.SummonForces(spawnType, isMiniWave);
    }

    private static void ResetOverride(string reason = "manual")
    {
        ClearNextSpawnOverrides(reason);
    }

    private static void ResetRuntimeState(string reason)
    {
        ResetOverride(reason);
        Disable = false;
        Instance?.ResetInstanceRuntimeState(reason);
    }

    private void ResetInstanceRuntimeState(string reason)
    {
        _defaultWaveGateOpen = true;

        if (_defaultWaveResetHandle.IsRunning)
            Timing.KillCoroutines(_defaultWaveResetHandle);

        Log.Debug($"SpawnSystem: Runtime state reset ({reason}).");
    }

    private static void OnRoundRestarting()
    {
        ResetRuntimeState(nameof(OnRoundRestarting));
    }

    private static void OnWaitingForPlayers()
    {
        ResetRuntimeState(nameof(OnWaitingForPlayers));
    }

    // =====================
    //  RespawningTeam
    // =====================

    public void SpawnHandler(RespawningTeamEventArgs ev)
    {
        // 常に最初に IsAllowed = false にする
        ev.IsAllowed = false;

        if (Disable)
        {
            Log.Debug("SpawnSystem: RespawningTeam ignored because SpawnSystem is disabled.");
            return;
        }

        // 条件に一致する one-shot オーバーライドを優先度順で適用する。
        RegisteredNextWaveOverride pending = null;
        foreach (var candidate in PendingOverrides
                     .OrderByDescending(entry => entry.Rule.Priority)
                     .ThenBy(entry => entry.Sequence)
                     .ToArray())
        {
            try
            {
                if (candidate.Rule.Matches(ev))
                {
                    pending = candidate;
                    break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(
                    $"SpawnSystem: Override predicate failed. Registration={candidate.RegistrationId}, Source={candidate.Source}, Error={ex}");
            }
        }

        if (pending != null)
        {
            var overrideType = pending.Rule.SpawnType;
            var isMiniWave = pending.Rule.OverrideMiniWave ?? ev.Wave.IsMiniWave;
            RemoveNextSpawnOverride(pending.RegistrationId, "override consumed");

            Log.Info(
                $"SpawnSystem: Applying next-wave override. SourceSpawnableFaction={ev.Wave.SpawnableFaction}, SourceTeam={ev.NextKnownTeam}, SourceMini={ev.Wave.IsMiniWave}, Override={overrideType}, Mini={isMiniWave}, QueuedBy={pending.Source}");
            SummonForces(overrideType, isMiniWave);
            return;
        }

        if (!_defaultWaveGateOpen)
        {
            Log.Debug("SpawnSystem: RespawningTeam ignored because default wave gate is closed.");
            return;
        }

        SpawnTypeId? decided = null;

        if (ev.NextKnownTeam == Faction.FoundationStaff)
            decided = DecideFoundationStaffType(ev);
        else if (ev.NextKnownTeam == Faction.FoundationEnemy)
            decided = DecideFoundationEnemyType(ev);

        if (decided is null)
        {
            Log.Warn($"SpawnSystem: No spawn type decided for {ev.NextKnownTeam} (Mini:{ev.Wave.IsMiniWave}).");
            return;
        }

        SummonForces(decided.Value, ev.Wave.IsMiniWave);
    }

    // =====================
    //  Decide: FoundationStaff
    // =====================

    private SpawnTypeId? DecideFoundationStaffType(RespawningTeamEventArgs ev)
    {
        var ctx = SpawnContextRegistry.ActiveContext;
        if (ctx == null)
            return null;

        int scpCount = Player.List.Count(p => p.Role.Team == Team.SCPs);
        bool highThreat = Player.Count >= Config.PlayerThresholdHigh ||
                          scpCount      >= Config.ScpThresholdHigh;

        var baseWeights = new Dictionary<SpawnTypeId, int>(
            ev.Wave.IsMiniWave
                ? ctx.FoundationStaffMiniWaveWeights
                : ctx.FoundationStaffWaveWeights
        );

        if (highThreat)
        {
            if (ev.Wave.IsMiniWave)
            {
                if (baseWeights.ContainsKey(SpawnTypeId.MtfHdBackup))
                    baseWeights[SpawnTypeId.MtfHdBackup] *= 2;
            }
            else
            {
                if (baseWeights.ContainsKey(SpawnTypeId.MtfHdNormal))
                    baseWeights[SpawnTypeId.MtfHdNormal] *= 2;
            }
        }

        var args = new CustomSpawningEventArgs(
            ev,
            ctx,
            baseWeights,
            Faction.FoundationStaff,
            ev.Wave.IsMiniWave
        );

        Spawning?.Invoke(null, args);

        if (!args.IsAllowed)
            return null;

        if (args.SpawnType.HasValue)
            return args.SpawnType.Value;

        return PickWeightedSpawnType(args.ContextOverride);
    }

    // =====================
    //  Decide: FoundationEnemy
    // =====================

    private SpawnTypeId? DecideFoundationEnemyType(RespawningTeamEventArgs ev)
    {
        var ctx = SpawnContextRegistry.ActiveContext;
        if (ctx == null)
            return null;

        var baseWeights = new Dictionary<SpawnTypeId, int>(
            ev.Wave.IsMiniWave
                ? ctx.FoundationEnemyMiniWaveWeights
                : ctx.FoundationEnemyWaveWeights
        );

        var args = new CustomSpawningEventArgs(
            ev,
            ctx,
            baseWeights,
            Faction.FoundationEnemy,
            ev.Wave.IsMiniWave
        );

        // 3005/Fifthist などの特殊処理は全部ここにぶら下がるハンドラ側で書く
        Spawning?.Invoke(null, args);

        if (!args.IsAllowed)
            return null;

        if (args.SpawnType.HasValue)
            return args.SpawnType.Value;

        return PickWeightedSpawnType(args.ContextOverride);
    }

    // =====================
    //  SummonForces
    // =====================

    public void SummonForces(SpawnTypeId spawnType, bool isMiniWave)
    {
        CloseDefaultWaveGate();

        try
        {
            var specs = Player.List
                .Where(p =>
                    p.Role == RoleTypeId.Spectator &&
                    p.GetCustomRole() == CRoleTypeId.None)
                .ToList();

            int spawnCount = specs.Count;

            string cassieCallsign  = string.Empty;
            string displayCallsign = string.Empty;

            if (Config.NatoCallsign)
            {
                var nato = GenerateNatoCallsignFull();
                cassieCallsign  = nato.cassie;
                displayCallsign = nato.display;
            }

            AssignTeamRoles(
                spawnType,
                playerFilter: p => specs.Contains(p),
                fixedCount: null);

            Faction faction = spawnType switch
            {
                SpawnTypeId.MtfNtfNormal or SpawnTypeId.MtfNtfBackup
                    or SpawnTypeId.MtfHdNormal or SpawnTypeId.MtfHdBackup
                    or SpawnTypeId.MtfLastOperationNormal or SpawnTypeId.MtfLastOperationBackup
                    => Faction.FoundationStaff,

                SpawnTypeId.GoiChaosNormal or SpawnTypeId.GoiChaosBackup
                    or SpawnTypeId.GoiFifthistNormal or SpawnTypeId.GoiFifthistBackup
                    or SpawnTypeId.GoiGoCNormal or SpawnTypeId.GoiGoCBackup
                    => Faction.FoundationEnemy,

                _ => Faction.Unclassified
            };

            // Spawned イベントに渡す Args を組み立てる
            var ctx = SpawnContextRegistry.ActiveContext;
            var dummyWeights = new Dictionary<SpawnTypeId, int>(); // ここでは使わないが型上必要

            var spawnedArgs = new CustomSpawningEventArgs(
                sourceEventArgs: null,
                nowContext: ctx,
                baseWeights: dummyWeights,
                faction: faction,
                isMiniWave: isMiniWave)
            {
                SpawnType = spawnType,
                SpawnCount = spawnCount,
                CassieCallsign = cassieCallsign,
                DisplayCallsign = displayCallsign,
            };

            Spawned?.Invoke(null, spawnedArgs);
        }
        catch (Exception ex)
        {
            Log.Error($"SpawnSystem: SummonForces failed for {spawnType} (Mini:{isMiniWave}): {ex}");
        }
        finally
        {
            ScheduleDefaultWaveGateReopen();
        }
    }

    private void CloseDefaultWaveGate()
    {
        _defaultWaveGateOpen = false;

        if (_defaultWaveResetHandle.IsRunning)
            Timing.KillCoroutines(_defaultWaveResetHandle);
    }

    private void ScheduleDefaultWaveGateReopen()
    {
        _defaultWaveResetHandle = Timing.CallDelayed(RoleSpawnTimings.SpawnSystemDefaultWaveReset, () =>
        {
            if (_disposed)
                return;

            _defaultWaveGateOpen = true;
        });
    }

    // =====================
    //  汎用ロール割り当て
    // =====================

    private void AssignTeamRoles(
        SpawnTypeId spawnType,
        Func<Player, bool> playerFilter,
        int? fixedCount = null)
    {
        var ctx = SpawnContextRegistry.ActiveContext;
        if (ctx == null)
        {
            Log.Warn($"SpawnSystem: AssignTeamRoles skipped for {spawnType}; active spawn context is null.");
            return;
        }

        if (!ctx.RoleTables.TryGetValue(spawnType, out var table) || table.Count == 0)
        {
            Log.Warn($"SpawnSystem: AssignTeamRoles skipped for {spawnType}; role table is missing or empty.");
            return;
        }

        var candidates = Player.List
            .Where(playerFilter)
            .Shuffle()
            .ToList();

        if (candidates.Count == 0)
        {
            Log.Warn($"SpawnSystem: AssignTeamRoles skipped for {spawnType}; no eligible spectators.");
            return;
        }

        int targetCount;
        if (fixedCount.HasValue)
        {
            targetCount = Math.Min(fixedCount.Value, candidates.Count);
        }
        else
        {
            var ratio = Config.SpawnRatios.GetValueOrDefault(spawnType, 1.0f);
            targetCount = (int)Math.Truncate(candidates.Count * ratio);
            if (targetCount <= 0)
                targetCount = candidates.Count;
        }

        var slots = BuildSlots(table, targetCount);

        slots = slots.Shuffle().ToList();
        int assignCount = Math.Min(slots.Count, candidates.Count);
        if (assignCount == 0)
        {
            Log.Warn($"SpawnSystem: AssignTeamRoles skipped for {spawnType}; no slots were built for {targetCount} target(s).");
            return;
        }

        Log.Info($"SpawnSystem: Assigning {assignCount}/{candidates.Count} player(s) to {spawnType}.");

        for (int i = 0; i < assignCount; i++)
        {
            var player = candidates[i];
            var key    = slots[i];

            switch (key.Kind)
            {
                case SpawnRoleKind.Vanilla:
                    player.SetRole(key.Vanilla);
                    break;

                case SpawnRoleKind.Custom:
                    player.SetRole(key.Custom);
                    break;
            }
        }
    }

    // =====================
    //  スロット構築（修正済み）
    // =====================

    /// <summary>
    /// ロールテーブルから targetCount 枠分のスロットリストを構築する。
    ///
    /// アルゴリズム:
    ///   Step 1: guaranteed フラグのあるロールを登録順に関係なく先に1枠ずつ確保する。
    ///           targetCount を超えた場合は guaranteed であっても追加しない。
    ///   Step 2: 残り枠を maxCount を重みとした重み付き抽選で埋める。
    ///           ただし各ロールの割り当て済み数が maxCount の上限に達したら
    ///           それ以上は選ばれない。
    ///           全ロールが上限に達した場合は最大 maxCount のロールで補充する。
    /// </summary>
    private List<SpawnRoleKey> BuildSlots(
        Dictionary<SpawnRoleKey, (float maxCount, bool guaranteed)> table,
        int targetCount)
    {
        var slots      = new List<SpawnRoleKey>(targetCount);
        // 各ロールの割り当て済み数を追跡
        var assigned   = new Dictionary<SpawnRoleKey, int>();

        foreach (var key in table.Keys)
            assigned[key] = 0;

        // -----------------------------------------------
        // Step 1: guaranteed ロールを先に1枠ずつ確保
        // -----------------------------------------------
        foreach (var kvp in table)
        {
            if (slots.Count >= targetCount)
                break;

            var (maxCount, guaranteed) = kvp.Value;
            if (!guaranteed)
                continue;

            int max = (int)maxCount;
            if (max <= 0)
                continue;

            slots.Add(kvp.Key);
            assigned[kvp.Key]++;
        }

        // -----------------------------------------------
        // Step 2: 残り枠を重み付き抽選で埋める
        // -----------------------------------------------
        while (slots.Count < targetCount)
        {
            // まだ上限に達していないロールだけを候補にする
            var available = table
                .Where(kvp => assigned[kvp.Key] < (int)kvp.Value.maxCount)
                .ToList();

            if (!available.Any())
            {
                // 全ロールが上限到達 → maxCount 最大のロールで補充（フォールバック）
                var filler = table
                    .OrderByDescending(kvp => kvp.Value.maxCount)
                    .First().Key;
                slots.Add(filler);
                continue;
            }

            // 上限に達していないロールから均等抽選
            SpawnRoleKey picked = available[Random.Range(0, available.Count)].Key;

            slots.Add(picked);
            assigned[picked]++;
        }

        return slots;
    }

    private SpawnTypeId? PickWeightedSpawnType(Dictionary<SpawnTypeId, int> weights)
    {
        var valid = weights.Where(kvp => kvp.Value > 0).ToList();
        if (!valid.Any())
        {
            Log.Warn($"SpawnSystem: No valid spawn types in '{SpawnContextRegistry.ActiveContextName}'");
            return null;
        }

        int total = valid.Sum(kvp => kvp.Value);
        int roll  = Random.Range(0, total);

        int cum = 0;
        foreach (var kvp in valid)
        {
            cum += kvp.Value;
            if (roll < cum)
                return kvp.Key;
        }

        return valid.First().Key;
    }

    // Cassie用(NATO_Aなど)と表示用(ALPHA-05)を両方返す
    private (string cassie, string display) GenerateNatoCallsignFull()
    {
        List<string> NatoForce =
        [
            "NATO_A", "NATO_B", "NATO_C", "NATO_D", "NATO_E", "NATO_F", "NATO_G", "NATO_H", "NATO_I", "NATO_J",
            "NATO_K", "NATO_L", "NATO_M", "NATO_N", "NATO_O", "NATO_P", "NATO_Q", "NATO_R", "NATO_S", "NATO_T",
            "NATO_U", "NATO_V", "NATO_W", "NATO_X", "NATO_Y", "NATO_Z"
        ];

        List<string> NatoForceL =
        [
            "ALPHA", "BRAVO", "CHARLIE", "DELTA", "ECHO", "FOXTROT", "GOLF", "HOTEL", "INDIA", "JULIETT",
            "KILO", "LIMA", "MIKE", "NOVEMBER", "OSCAR", "PAPA", "QUEBEC", "ROMEO", "SIERRA", "TANGO",
            "UNIFORM", "VICTOR", "WHISKEY", "XRAY", "YANKEE", "ZULU"
        ];

        string natoForce  = NatoForce.RandomItem();
        string natoForceL = NatoForceL[NatoForce.IndexOf(natoForce)];
        int natoForceNum  = Random.Range(1, 20);

        return (natoForce, $"{natoForceL}-{natoForceNum:00}");
    }
}

// =====================
//  Shuffle拡張
// =====================

public static class EnumerableExtensions
{
    public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
    {
        var list = source.ToList();
        int n = list.Count;
        for (int i = 0; i < n - 1; i++)
        {
            int j = Random.Range(i, n);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }
}
