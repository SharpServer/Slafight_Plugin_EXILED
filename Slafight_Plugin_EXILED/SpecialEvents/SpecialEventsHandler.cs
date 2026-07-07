using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using MEC;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Features.RoundVictory.Core;
using Slafight_Plugin_EXILED.Changes;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.MainHandlers;
using Random = UnityEngine.Random;
using Slafight_Plugin_EXILED.API.Interface;

namespace Slafight_Plugin_EXILED.SpecialEvents;

public static class LinqExtensions
{
    // List<T> 用「直近 n 件を取る」互換メソッド
    public static IEnumerable<T> TakeLastCompat<T>(this IList<T> source, int count)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (count <= 0) yield break;

        int start = Math.Max(0, source.Count - count);
        for (int i = start; i < source.Count; i++)
            yield return source[i];
    }
}

public class SpecialEventsHandler : IBootstrapHandler, IDisposable
{
    private const int DefaultEventWeight = 10;

    private static readonly IReadOnlyDictionary<SpecialEventType, int> EventWeights =
        new Dictionary<SpecialEventType, int>
        {
            [SpecialEventType.OmegaWarhead] = 5,
            [SpecialEventType.EndlessCry] = 7,
            [SpecialEventType.Scp1509BattleField] = 6,
            [SpecialEventType.FifthistsRaid] = 10,
            [SpecialEventType.NuclearAttack] = 10,
            [SpecialEventType.ClassicEvent] = 0,
            [SpecialEventType.OperationBlackout] = 0,
            [SpecialEventType.SnowWarriorsAttack] = 10,
            [SpecialEventType.FacilityTermination] = 4,
            [SpecialEventType.RevolverBattles] = 0,
            [SpecialEventType.SergeyMakarovReturns] = 66,
            [SpecialEventType.SpeedUpEvent] = 6,
            [SpecialEventType.DailyFoundation] = 0,
            [SpecialEventType.CandyWarriorsAttack] = 10,
            [SpecialEventType.CaseColourlessGreen] = 10,
        };

    public static SpecialEventsHandler Instance { get; private set; }
    public static void Register()
    {
        Unregister();
        _ = new SpecialEventsHandler();
    }

    public static void Unregister()
    {
        Instance?.Dispose();
        Instance = null;
    }

    private bool _disposed;

    public SpecialEventsHandler()
    {
        Instance = this;
        SpecialEvent.RegisterAllEvents(); // 全イベント自動登録
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        SpecialEvent.UnregisterAllEvents();
        EventQueue.Clear();
        HappenedEvents.Clear();
        CurrentEvent = SpecialEventType.None;
        Instance = null;
        GC.SuppressFinalize(this);
    }

    // =====================
    //  イベントキューと状態
    // =====================

    public readonly List<SpecialEventType> EventQueue = [];

    /// <summary>
    /// 発火済みイベントの履歴（古い順）。
    /// 直近 EventCooldownCount 件に含まれるイベントは抽選から除外される。
    /// </summary>
    public readonly List<SpecialEventType> HappenedEvents = [];

    /// <summary>
    /// 同一イベントが再抽選されるまでの抽選スキップ回数。
    /// </summary>
    public const int EventCooldownCount = 3;

    public int EventPID = 1;
    public SpecialEventType CurrentEvent { get; private set; } = SpecialEventType.None;
    public SpecialEventType NowEvent => CurrentEvent;

    public bool IsFifthistsRaidActive { get; set; }
    float chance = 1f / 2f;

    // =====================
    //  イベント操作
    // =====================

    public void AddEvent(SpecialEventType eventType)
    {
        if (!Enum.IsDefined(typeof(SpecialEventType), eventType))
        {
            Log.Error($"SEH: AddEvent failed (invalid SpecialEventType: {eventType})");
            return;
        }

        if (!SpecialEvent.IsEventExecutable(eventType))
        {
            Log.Warn($"SEH: AddEvent skipped (not executable: {eventType})");
            return;
        }

        EventQueue.Add(eventType);
        EventLocSet();
        Log.Info($"SEH: Added event to queue: {eventType}");
    }

    public void SkipEvent(int index = 0)
    {
        if (index < 0 || index >= EventQueue.Count)
        {
            Log.Warn($"SEH: SkipEvent failed (invalid index: {index})");
            return;
        }

        var removed = EventQueue[index];
        EventQueue.RemoveAt(index);
        RecordHappenedEvent(removed);
        EventPID++;
        EventLocSet();
        Log.Info($"SEH: Skipped event: {removed}");
    }

    public void RunEvent(SpecialEventType eventType)
    {
        if (!Enum.IsDefined(typeof(SpecialEventType), eventType))
        {
            Log.Error($"SEH: RunEvent failed (invalid SpecialEventType: {eventType})");
            return;
        }

        EventQueue.Clear();
        EventQueue.Add(eventType);
        SpecialEventsController();
    }

    public void RunRandomEvent()
    {
        if (Random.value > chance)
        {
            Log.Info("SEH: RunRandomEvent rolled None (no event executed).");
            return;
        }

        SelectRandom();
        if (SelectedEvent == SpecialEventType.None)
        {
            Log.Info("SEH: RunRandomEvent skipped (no executable events).");
            return;
        }

        RunEvent(SelectedEvent);
    }

    public void SetQueueEvent(SpecialEventType eventType)
    {
        if (!Enum.IsDefined(typeof(SpecialEventType), eventType))
        {
            Log.Error($"SEH: SetQueueEvent failed (invalid SpecialEventType: {eventType})");
            return;
        }

        if (EventQueue.Count == 0)
            EventQueue.Add(eventType);
        else
            EventQueue[0] = eventType;

        EventLocSet();
        Log.Info($"SEH: Queue set to: {eventType}");
    }

    public void SetQueueRandomEvent()
    {
        if (Random.value > chance)
        {
            if (EventQueue.Count == 0)
                EventQueue.Add(SpecialEventType.None);
            else
                EventQueue[0] = SpecialEventType.None;

            EventLocSet();
            Log.Info("SEH: SetQueueRandomEvent rolled None (Queue[0] set to None).");
            return;
        }

        SelectRandom();
        if (SelectedEvent == SpecialEventType.None)
        {
            Log.Info("SEH: SetQueueRandomEvent skipped (no executable events).");
            return;
        }

        if (EventQueue.Count == 0)
            EventQueue.Add(SelectedEvent);
        else
            EventQueue[0] = SelectedEvent;

        EventLocSet();
        Log.Info($"SEH: Queue[0] rerolled to: {SelectedEvent}");
    }

    public void InsertQueueRandomEventAfterFirst()
    {
        if (Random.value > chance)
        {
            Log.Info("SEH: InsertQueueRandomEventAfterFirst rolled None (no insert).");
            return;
        }

        SelectRandom();
        if (SelectedEvent == SpecialEventType.None)
        {
            Log.Info("SEH: InsertQueueRandomEventAfterFirst skipped (no executable events).");
            return;
        }

        int index = Math.Min(1, EventQueue.Count);
        EventQueue.Insert(index, SelectedEvent);

        EventLocSet();
        Log.Info($"SEH: Queue insert random at index {index}: {SelectedEvent}");
    }

    // =====================
    //  内部処理
    // =====================

    private SpecialEventType SelectedEvent = SpecialEventType.None;

    private void SelectRandom()
    {
        var allowedEvents = GetAllowedEvents();

        if (!allowedEvents.Any())
        {
            SelectedEvent = SpecialEventType.None;
            return;
        }

        // 重み付きプールを構築（重み0のイベントは除外）
        var pool = new List<SpecialEventType>();
        foreach (var ev in allowedEvents)
        {
            int w = GetEventWeight(ev);
            for (int i = 0; i < w; i++)
                pool.Add(ev);
        }

        if (pool.Count == 0)
        {
            // 全イベントが重み0の場合は均等フォールバック
            SelectedEvent = allowedEvents[Random.Range(0, allowedEvents.Count)];
            return;
        }

        SelectedEvent = pool[Random.Range(0, pool.Count)];
    }

    private static int GetEventWeight(SpecialEventType type)
    {
        if (!EventWeights.TryGetValue(type, out int weight))
            return DefaultEventWeight;

        return Math.Max(0, weight);
    }

    /// <summary>
    /// 実行可能かつ直近 EventCooldownCount 回の抽選に含まれないイベントの一覧を返す。
    /// 全候補がクールダウン中の場合は空リストを返す（None を返すのは呼び出し側の責務）。
    /// </summary>
    private List<SpecialEventType> GetAllowedEvents()
    {
        var recentEvents = new HashSet<SpecialEventType>(
            HappenedEvents.TakeLastCompat(EventCooldownCount)
        );

        // HappenedEvents への記録タイミングがどうズレても
        // CurrentEvent と同じイベントは絶対に選ばれないようにする保険
        if (CurrentEvent != SpecialEventType.None)
            recentEvents.Add(CurrentEvent);

        var allowed = new List<SpecialEventType>();
        foreach (SpecialEventType type in Enum.GetValues(typeof(SpecialEventType)))
        {
            if (type == SpecialEventType.None) continue;
            if (!SpecialEvent.IsEventExecutable(type)) continue;
            if (recentEvents.Contains(type)) continue;
            allowed.Add(type);
        }

        return allowed;
    }

    /// <summary>
    /// 発火済み履歴にイベントを追加する。
    /// 履歴は EventCooldownCount * 2 件でトリムして無制限に膨らまないよう管理する。
    /// </summary>
    private void RecordHappenedEvent(SpecialEventType eventType)
    {
        HappenedEvents.Add(eventType);

        int maxHistory = EventCooldownCount * 2;
        if (HappenedEvents.Count > maxHistory)
            HappenedEvents.RemoveRange(0, HappenedEvents.Count - maxHistory);
    }

    public void SpecialEventsController()
    {
        if (EventQueue.Count == 0)
        {
            Log.Warn("SEH: Empty queue");
            return;
        }

        var eventType = EventQueue[0];

        if (eventType == SpecialEventType.None)
        {
            EventQueue.RemoveAt(0);
            EventLocSet();
            Log.Debug("SEH: None event dequeued, no action.");
            return;
        }

        var specialEvent = SpecialEvent.GetEvent(eventType);
        if (specialEvent == null)
        {
            Log.Error($"SEH: No implementation for {eventType}");
            return;
        }

        InitStats();

        CurrentEvent = eventType;
        EventLocSet();

        specialEvent.Execute(EventPID);
        Log.Info($"SEH: Executed {eventType}: {specialEvent.LocalizedName}");

        RecordHappenedEvent(eventType);

        if (EventQueue.Count > 0)
            EventQueue.RemoveAt(0);

        EventLocSet();
    }

    public void InitStats()
    {
        EventPID++;
        ResetRoundScopedState();
    }

    private void ResetRoundScopedState()
    {
        RoundHazardController.ResetRoundState();
        EscapeHandler.ClearEscapeOverrides();
        EvacuationRoundEndState.Reset();
        SpawnSystem.Disable = false;
        if (SpawnContextRegistry.TryGet("Default", out _))
            SpawnContextRegistry.SetActive("Default");
        MapFlags.IsOverrideActivated = false;
        IsFifthistsRaidActive = false;
        NewEventHandler.ResetState();
    }

    // =====================
    //  ラウンド系イベントハンドラ
    // =====================

    public void RoundStartedAddEvent()
    {
        Timing.CallDelayed(0.1f, SpecialEventsController);
    }

    public void RoundRestartSkipEvent()
    {
        EventPID++;
        ResetRoundScopedState();

        // CurrentEvent を None にクリアする前に履歴へ記録
        // （RoundRestartAddEvent → SelectRandom が走る前に HappenedEvents へ確実に入れる）
        if (CurrentEvent != SpecialEventType.None)
            RecordHappenedEvent(CurrentEvent);

        CurrentEvent = SpecialEventType.None;

        if (EventQueue.Count <= 1)
            return;

        SkipEvent(); // キュー先頭のイベントも別途記録される（二重記録だが実害なし）
    }

    public void RoundRestartAddEvent()
    {
        SelectRandom();
        Timing.CallDelayed(0.01f, () => AddEvent(SelectedEvent));
    }

    public void OnWaitingForPlayersInitEvent()
    {
        ResetRoundScopedState();

        if (EventQueue.Count == 0)
        {
            SelectRandom();
            AddEvent(SelectedEvent);
        }

        EventLocSet();
    }

    // =====================
    //  ローカライズ
    // =====================

    public string LocalizedEventName { get; private set; } = "無し";
    public string EventNeedTriggers { get; private set; } = "無し";

    public void EventLocSet()
    {
        SpecialEventType type;

        if (CurrentEvent != SpecialEventType.None)
            type = CurrentEvent;
        else if (EventQueue.Count == 0)
        {
            LocalizedEventName = "無し";
            EventNeedTriggers = "無し";
            return;
        }
        else
            type = EventQueue[0];

        var ev = SpecialEvent.GetEvent(type);
        LocalizedEventName = ev?.LocalizedName ?? "無し";
        EventNeedTriggers = ev?.TriggerRequirement ?? "無し";
    }

    // =====================
    //  API
    // =====================

    public static bool IsWarheadable()
    {
        var nowEvent = Instance?.NowEvent ?? SpecialEventType.None;
#pragma warning disable CS0618
        return nowEvent switch
        {
            SpecialEventType.OmegaWarhead or
                SpecialEventType.OldDeltaWarhead or
                SpecialEventType.NuclearAttack or
                SpecialEventType.OperationBlackout or
                SpecialEventType.Scp1509BattleField or
                SpecialEventType.SnowWarriorsAttack or
                SpecialEventType.CaseColourlessGreen or
                SpecialEventType.FacilityTermination or 
                SpecialEventType.WaterWarriorsRaid or 
                SpecialEventType.CandyWarriorsAttack => false,
            _ => true
        };
#pragma warning restore CS0618
    }

    // =====================
    //  互換用 Obsolete ラッパー
    // =====================

    [Obsolete("Use AddEvent(SpecialEventType eventType) instead.")]
    public void Add(SpecialEventType eventType) => AddEvent(eventType);

    [Obsolete("Use SkipEvent(int index = 0) instead.")]
    public void Skip() => SkipEvent();

    [Obsolete("Use RunEvent(SpecialEventType eventType) instead.")]
    public void ForceRun(SpecialEventType eventType) => RunEvent(eventType);

    [Obsolete("Use RunRandomEvent() instead.")]
    public void ForceRunRandom() => RunRandomEvent();

    [Obsolete("Use SetQueueEvent(SpecialEventType eventType) instead.")]
    public void ForceNext(SpecialEventType eventType) => SetQueueEvent(eventType);

    [Obsolete("Use SetQueueRandomEvent() instead.")]
    public void QueueRandom() => SetQueueRandomEvent();

    [Obsolete("Use SetQueueEvent(...) and SpecialEventsController() instead.")]
    public void LegacyRun(SpecialEventType eventType)
    {
        SetQueueEvent(eventType);
        SpecialEventsController();
    }
}
