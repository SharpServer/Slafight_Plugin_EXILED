using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.SpecialEvents;
using Player = Exiled.API.Features.Player;

namespace Slafight_Plugin_EXILED.API.Features;

public abstract class SpecialEvent
{
    // 全インスタンスを追跡（重複登録防止）
    private static readonly HashSet<SpecialEvent> RegisteredInstances = [];
    private static readonly List<Type> EventTypes;
    private static readonly Dictionary<SpecialEventType, SpecialEvent> EventTypeToInstance = new();
    private static readonly Dictionary<SpecialEventType, int> MinPlayersCache = new();  // ★静的キャッシュ追加
    private static bool _eventsSubscribed;

    static SpecialEvent()
    {
        var asm = typeof(SpecialEvent).Assembly;
        EventTypes = asm.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(SpecialEvent)) && !t.IsAbstract)
            .Where(t => t.GetCustomAttributes(typeof(SpecialEventAutoRegisterIgnoreAttribute), true).Length == 0)
            .ToList();
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class SpecialEventAutoRegisterIgnoreAttribute : Attribute { }

    /// <summary>
    /// このイベントのタイプ。
    /// </summary>
    public abstract SpecialEventType EventType { get; }

    /// <summary>
    /// イベント実行時の最小プレイヤー数要件（0 = 無制限）。
    /// </summary>
    public virtual int MinPlayersRequired => 0;

    /// <summary>
    /// ローカライズ名。
    /// </summary>
    public abstract string LocalizedName { get; }

    /// <summary>
    /// トリガー条件文字列。
    /// </summary>
    public abstract string TriggerRequirement { get; }

    /// <summary>
    /// 人数以外の独自実行条件（派生クラスでoverride）。
    /// </summary>
    public virtual bool IsReadyToExecute() => true;

    /// <summary>
    /// イベント実行可否判定（人数 + Readyを統合）。
    /// </summary>
    public virtual bool IsEventExecutable()
    {
        return Player.List.Count(p => p.IsNotHost()) >= MinPlayersRequired && IsReadyToExecute();
    }

    /// <summary>
    /// SpecialEventsHandler が現在の PID を渡して呼ぶ入口。
    /// 外部 API としては public のままにする。
    /// </summary>
    public void Execute(int eventPid)
    {
        CurrentEventPid = eventPid;
        OnExecute(eventPid);
    }

    /// <summary>
    /// 派生クラス側が実装する実体。基本はこちらを override する。
    /// </summary>
    protected abstract void OnExecute(int eventPid);

    /// <summary>
    /// このイベントインスタンスにとって「有効だった」時の EventPID。
    /// 遅延実行やコルーチン内でのキャンセル判定に使う。
    /// </summary>
    public int CurrentEventPid { get; private set; }

    /// <summary>
    /// EventPID が変わっていたら、またはロビー中なら true（=キャンセルすべき）。
    /// </summary>
    public bool IsCanceled()
    {
        if (Round.IsLobby)
            return true;
        return CurrentEventPid != SpecialEventsHandler.Instance.EventPID;
    }

    /// <summary>
    /// 便利メソッド。if (CancelIfOutdated()) return; みたいに使う。
    /// </summary>
    protected bool CancelIfOutdated()
    {
        return IsCanceled();
    }

    /// <summary>
    /// イベント初期化（オプション）。
    /// </summary>
    public virtual void OnRegister() { }

    /// <summary>
    /// イベント登録時に任意でサブスクライブするイベント。
    /// </summary>
    public virtual void RegisterEvents() { }

    /// <summary>
    /// イベント登録解除時に任意でアンスバスクライブ。
    /// </summary>
    public virtual void UnregisterEvents() { }

    // ==== 静的メソッド ====
    public static void RegisterAllEvents()
    {
        if (!_eventsSubscribed)
        {
            Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;
            Exiled.Events.Handlers.Server.RestartingRound += OnRestartingRound;
            Exiled.Events.Handlers.Server.WaitingForPlayers += OnWaitingForPlayers;
            _eventsSubscribed = true;
        }

        foreach (var type in EventTypes)
        {
            try
            {
                var instance = (SpecialEvent)Activator.CreateInstance(type);
                instance.InternalRegister();
            }
            catch (Exception ex)
            {
                Log.Error($"SpecialEvent.RegisterAllEvents failed for {type.Name}: {ex}");
            }
        }
    }

    public static void UnregisterAllEvents()
    {
        foreach (var instance in RegisteredInstances.ToList())
            instance.InternalUnregister();

        RegisteredInstances.Clear();
        EventTypeToInstance.Clear();
        MinPlayersCache.Clear();  // ★キャッシュクリア

        if (_eventsSubscribed)
        {
            Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;
            Exiled.Events.Handlers.Server.RestartingRound -= OnRestartingRound;
            Exiled.Events.Handlers.Server.WaitingForPlayers -= OnWaitingForPlayers;
            _eventsSubscribed = false;
        }
    }

    // ==== 共通イベントハンドラ ====
    private static void OnRoundStarted()
    {
        SpecialEventsHandler.Instance?.RoundStartedAddEvent();
    }

    private static void OnRestartingRound()
    {
        SpecialEventsHandler.Instance?.RoundRestartSkipEvent();
        SpecialEventsHandler.Instance?.RoundRestartAddEvent();
    }

    private static void OnWaitingForPlayers()
    {
        SpecialEventsHandler.Instance?.OnWaitingForPlayersInitEvent();
    }

    // ==== インスタンス管理 ====
    private void InternalRegister()
    {
        if (RegisteredInstances.Add(this))
        {
            EventTypeToInstance[EventType] = this;
            MinPlayersCache[EventType] = MinPlayersRequired;  // ★登録時キャッシュ
            OnRegister();
            RegisterEvents();
            Log.Debug($"SpecialEvent registered: {GetType().Name} ({EventType})");
        }
    }

    private void InternalUnregister()
    {
        if (RegisteredInstances.Remove(this))
        {
            if (EventTypeToInstance.TryGetValue(EventType, out var inst) && ReferenceEquals(inst, this))
            {
                EventTypeToInstance.Remove(EventType);
                MinPlayersCache.Remove(EventType);  // ★キャッシュ削除
            }

            UnregisterEvents();
            Log.Debug($"SpecialEvent unregistered: {GetType().Name}");
        }
    }

    // ==== 静的ヘルパー ====
    public static SpecialEvent GetEvent(SpecialEventType eventType)
    {
        return EventTypeToInstance.GetValueOrDefault(eventType);
    }

    /// ★完全版: staticで人数キャッシュ + Ready自動呼び出し
    public static bool IsEventExecutable(SpecialEventType eventType)
    {
        var ev = GetEvent(eventType);
        if (ev == null) return false;
            
        var minPlayers = MinPlayersCache.GetValueOrDefault(eventType, 0);
        return Player.List.Count >= minPlayers && ev.IsReadyToExecute();
    }
}