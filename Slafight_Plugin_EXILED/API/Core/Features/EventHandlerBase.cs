using System;
using System.Collections.Generic;
using System.Reflection;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using LabApi.Events.CustomHandlers;
using Slafight_Plugin_EXILED.API.Core.Enums;
using Slafight_Plugin_EXILED.API.Core.Features.Attributes;
using Slafight_Plugin_EXILED.API.Core.Interfaces;

using PlayerHandlers = Exiled.Events.Handlers.Player;
using ServerHandlers = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// EXILED と LabApi のイベントを購読するハンドラの基底です。派生するだけで <see cref="EventHandlerRegistry"/> が自動登録します。
/// </summary>
/// <remarks>自動登録を避けるには <see cref="NoAutoRegisterAttribute"/> を付けてください。</remarks>
public abstract class EventHandlerBase : CustomEventsHandler, IDisposable
{
    private static readonly List<EventHandlerBase> ActiveHandlers = [];

    private static bool lifetimeHooked;

    /// <summary>
    /// 一度でも有効化されたか。<see cref="Lifetime"/> を読むためだけの実体で <see cref="OnDisposed"/> を走らせないための印です。
    /// </summary>
    private bool wasEnabled;

    /// <summary>
    /// 現在有効になっているハンドラの一覧です。
    /// </summary>
    public static IReadOnlyList<EventHandlerBase> Active => ActiveHandlers;

    /// <summary>
    /// このハンドラが現在イベントを購読しているかどうか。
    /// </summary>
    public bool IsEnabled { get; private set; }

    /// <summary>
    /// このハンドラが破棄済みかどうか。破棄後は <see cref="Enable"/> できません。
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// 自動破棄のタイミング。既定は <see cref="HandlerLifetime.Manual"/> (自動破棄しない)。
    /// </summary>
    public virtual HandlerLifetime Lifetime => HandlerLifetime.Manual;

    /// <summary>
    /// 所有プレイヤーです。退出時に自動で <see cref="Dispose"/> されます。既定は <see cref="IPlayerOwn"/> の Player です。
    /// </summary>
    public virtual Player Owner => (this as IPlayerOwn)?.Player;

    /// <summary>
    /// ハンドラを生成してそのまま <see cref="Enable"/> します。
    /// 自動登録を使わず、任意のタイミングで生成したい場合に使います。
    /// </summary>
    public static T Create<T>() where T : EventHandlerBase, new()
    {
        T handler = new T();
        handler.Enable();

        return handler;
    }

    /// <summary>
    /// 有効な全ハンドラを破棄します。
    /// 通常は <see cref="EventHandlerRegistry.Shutdown"/> から呼ばれます。
    /// </summary>
    public static void DisposeAll()
    {
        foreach (EventHandlerBase handler in ActiveHandlers.ToArray())
        {
            handler.Dispose();
        }
    }

    /// <summary>
    /// 指定したアセンブリが生成したハンドラだけを破棄します。
    /// </summary>
    public static void DisposeAssembly(Assembly assembly)
    {
        if (assembly is null) return;

        foreach (EventHandlerBase handler in ActiveHandlers.ToArray())
        {
            if (handler.GetType().Assembly == assembly)
                handler.Dispose();
        }
    }

    /// <summary>
    /// EXILED / LabApi の static イベントへの購読をここで書きます。
    /// <see cref="Enable"/> から 1 回だけ呼ばれます。
    /// </summary>
    public virtual void RegisterEvents()
    {
    }

    /// <summary>
    /// <see cref="RegisterEvents"/> で購読したものをここで解除します。
    /// <see cref="Disable"/> から 1 回だけ呼ばれます。
    /// </summary>
    public virtual void UnregisterEvents()
    {
    }

    /// <summary>
    /// イベントの購読を開始します。二重に呼んでも安全です。
    /// </summary>
    public void Enable()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(GetType().Name);

        if (IsEnabled) return;

        // CustomEventsHandler の override 分は LabApi 側がリフレクションで購読する。
        CustomHandlersManager.RegisterEventsHandler(this);

        try
        {
            RegisterEvents();
        }
        catch (Exception exception)
        {
            // 途中で失敗した場合、購読済みのものを巻き戻してから投げ直す。
            SafeInvoke(UnregisterEvents, nameof(UnregisterEvents));
            CustomHandlersManager.UnregisterEventsHandler(this);
            Log.Error($"[Slafight] [{GetType().Name}] {nameof(RegisterEvents)} に失敗しました: {exception}");

            throw;
        }

        IsEnabled = true;
        wasEnabled = true;
        Track(this);
        SafeInvoke(OnEnabled, nameof(OnEnabled));
    }

    /// <summary>
    /// イベントの購読を解除します。二重に呼んでも安全です。
    /// </summary>
    public void Disable()
    {
        if (!IsEnabled) return;

        // 解除処理中に再入しても走らないよう、先にフラグを落とす。
        IsEnabled = false;

        SafeInvoke(UnregisterEvents, nameof(UnregisterEvents));
        CustomHandlersManager.UnregisterEventsHandler(this);
        Untrack(this);
        SafeInvoke(OnDisabled, nameof(OnDisabled));
    }

    /// <summary>
    /// 購読を解除してこのハンドラを破棄します。二重に呼んでも安全です。
    /// </summary>
    public void Dispose()
    {
        if (IsDisposed) return;

        Disable();
        IsDisposed = true;

        // 一度も購読していないなら後始末するものは無い。
        // ここで呼ぶと、Lifetime を読むためだけに作られた実体でも利用者の処理が走る。
        if (wasEnabled)
            SafeInvoke(OnDisposed, nameof(OnDisposed));
    }

    /// <summary>
    /// 購読完了後に呼ばれます。
    /// </summary>
    protected virtual void OnEnabled()
    {
    }

    /// <summary>
    /// 購読解除後に呼ばれます。
    /// </summary>
    protected virtual void OnDisabled()
    {
    }

    /// <summary>
    /// 破棄時に呼ばれます。ハンドラ以外のリソース (コルーチン・生成物など) の後始末に使います。
    /// </summary>
    protected virtual void OnDisposed()
    {
    }

    /// <summary>
    /// 後始末系の処理は 1 つ落ちても他を巻き込まないようにする。
    /// </summary>
    private void SafeInvoke(Action action, string name)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            Log.Error($"[Slafight] [{GetType().Name}] {name} で例外が発生しました: {exception}");
        }
    }

    private static void Track(EventHandlerBase handler)
    {
        ActiveHandlers.Add(handler);

        if (lifetimeHooked) return;

        ServerHandlers.RestartingRound += OnLifetimeRestartingRound;
        PlayerHandlers.Left += OnLifetimePlayerLeft;
        lifetimeHooked = true;
    }

    private static void Untrack(EventHandlerBase handler)
    {
        ActiveHandlers.Remove(handler);

        if (!lifetimeHooked || ActiveHandlers.Count > 0) return;

        ServerHandlers.RestartingRound -= OnLifetimeRestartingRound;
        PlayerHandlers.Left -= OnLifetimePlayerLeft;
        lifetimeHooked = false;
    }

    private static void OnLifetimeRestartingRound()
    {
        foreach (EventHandlerBase handler in ActiveHandlers.ToArray())
        {
            if (handler.Lifetime is HandlerLifetime.Round)
            {
                handler.Dispose();
            }
        }
    }

    /// <remarks>netId ではなく <see cref="ReferenceHub"/> を直接比べます。</remarks>
    private static void OnLifetimePlayerLeft(LeftEventArgs ev)
    {
        if (ev.Player?.ReferenceHub is not { } hub) return;

        foreach (EventHandlerBase handler in ActiveHandlers.ToArray())
        {
            if (handler.Owner is { } owner && ReferenceEquals(owner.ReferenceHub, hub))
            {
                handler.Dispose();
            }
        }
    }
}
