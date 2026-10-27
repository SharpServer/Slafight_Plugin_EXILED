using System;
using System.Collections.Generic;
using Exiled.API.Features;
using MEC;

using ServerHandlers = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// ラウンドに紐づくコルーチンと後始末のスコープです。ラウンド再開時に必ず片付きます。
/// </summary>
public sealed class RoundScope
{
    private static RoundScope current;
    private static bool hooked;

    private readonly List<CoroutineHandle> coroutines = [];
    private readonly List<Action> cleanups = [];

    private RoundScope()
    {
    }

    /// <summary>
    /// 現在のラウンドのスコープです。ラウンド再開のたびに新しくなります。
    /// </summary>
    public static RoundScope Current
    {
        get
        {
            Hook();

            return current ??= new RoundScope();
        }
    }

    /// <summary>
    /// このスコープが既に閉じられているかどうか。
    /// </summary>
    public bool IsClosed { get; private set; }

    /// <summary>
    /// 一定間隔で実行し続けます。ラウンドが終わると止まります。
    /// </summary>
    public CoroutineHandle RunLoop(float interval, Action tick)
    {
        if (tick is null)
            throw new ArgumentNullException(nameof(tick));

        return Track(Timing.RunCoroutine(Loop(interval, tick)));
    }

    /// <summary>
    /// 指定秒後に一度だけ実行します。ラウンドが終わっていれば実行されません。
    /// </summary>
    public CoroutineHandle Delay(float delay, Action action)
    {
        if (action is null)
            throw new ArgumentNullException(nameof(action));

        return Track(Timing.CallDelayed(delay, () =>
        {
            if (IsClosed) return;

            Invoke(action);
        }));
    }

    /// <summary>
    /// 自前で起こしたコルーチンをこのスコープに預けます。ラウンド終了時に一緒に止まります。
    /// </summary>
    public CoroutineHandle Track(CoroutineHandle handle)
    {
        if (IsClosed)
        {
            Timing.KillCoroutines(handle);

            return handle;
        }

        coroutines.Add(handle);

        return handle;
    }

    /// <summary>
    /// ラウンド終了時に実行する後始末を登録します。登録の逆順で実行されます。
    /// </summary>
    public void OnEnd(Action cleanup)
    {
        if (cleanup is null)
            throw new ArgumentNullException(nameof(cleanup));

        if (IsClosed)
        {
            Invoke(cleanup);

            return;
        }

        cleanups.Add(cleanup);
    }

    /// <summary>
    /// 現在のラウンドスコープを閉じ、静的イベント購読を解除します。
    /// </summary>
    public static void Shutdown()
    {
        OnRestartingRound();

        if (!hooked) return;

        ServerHandlers.RestartingRound -= OnRestartingRound;
        hooked = false;
    }

    private IEnumerator<float> Loop(float interval, Action tick)
    {
        while (!IsClosed)
        {
            yield return Timing.WaitForSeconds(interval);

            if (IsClosed) yield break;

            Invoke(tick);
        }
    }

    private void Close()
    {
        if (IsClosed) return;

        IsClosed = true;

        foreach (CoroutineHandle handle in coroutines)
        {
            Timing.KillCoroutines(handle);
        }

        coroutines.Clear();

        // 1 つ落ちても残りを巻き込まないよう、後ろから 1 件ずつ。
        for (int i = cleanups.Count - 1; i >= 0; i--)
        {
            Invoke(cleanups[i]);
        }

        cleanups.Clear();
    }

    private static void Invoke(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            Log.Error($"[Slafight] RoundScope の処理で例外が発生しました: {exception}");
        }
    }

    private static void Hook()
    {
        if (hooked) return;

        hooked = true;

        ServerHandlers.RestartingRound += OnRestartingRound;
    }

    private static void OnRestartingRound()
    {
        current?.Close();
        current = null;
    }
}
