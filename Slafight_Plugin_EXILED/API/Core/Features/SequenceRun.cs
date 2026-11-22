using System;
using System.Collections.Generic;
using MEC;

namespace Slafight_Plugin_EXILED.API.Core.Features;

public enum SequenceEndReason
{
    Completed,
    Cancelled,
    Overridden,
    RoundEnded,
    PluginDisabled,
    Failed,
}

/// <summary>一度の実行に紐づく寿命です。同じシーケンスを起動し直すと別の実行になります。</summary>
public sealed class SequenceRun
{
    private readonly List<Action> cleanups = new();

    internal SequenceRun(SequenceBase sequence) => Sequence = sequence;

    public SequenceBase Sequence { get; }
    public bool IsActive => ReferenceEquals(SequenceManager.CurrentRun, this);
    internal SequenceEndReason? EndReason { get; private set; }

    public bool Complete() => SequenceManager.TryEnd(this, SequenceEndReason.Completed);
    public bool Cancel() => SequenceManager.TryEnd(this, SequenceEndReason.Cancelled);

    /// <summary>実行中だけ動く遅延処理を登録します。処理が例外を投げるとシーケンスを終了します。</summary>
    public CoroutineHandle Delay(float seconds, Action action)
    {
        if (action is null)
            throw new ArgumentNullException(nameof(action));

        if (!IsActive)
            return default;

        return Track(Timing.CallDelayed(seconds, () =>
        {
            if (!IsActive)
                return;

            try
            {
                action();
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }));
    }

    /// <summary>
    /// この実行に紐づく MEC コルーチンを開始します。例外が起きると実行全体が終了します。
    /// 正常にシーケンスを完了する箇所では、明示的に <see cref="Complete"/> を呼びます。
    /// </summary>
    public CoroutineHandle RunCoroutine(IEnumerator<float> coroutine)
    {
        if (coroutine is null)
            throw new ArgumentNullException(nameof(coroutine));

        bool advancing = false;
        bool disposed = false;
        OnEnd(() =>
        {
            if (!advancing)
                DisposeCoroutine();
        });

        return IsActive ? Track(Timing.RunCoroutine(Run())) : default;

        IEnumerator<float> Run()
        {
            try
            {
                while (IsActive)
                {
                    float wait;
                    advancing = true;
                    try
                    {
                        if (!coroutine.MoveNext() || !IsActive)
                            yield break;

                        wait = coroutine.Current;
                    }
                    catch (Exception exception)
                    {
                        Fail(exception);
                        yield break;
                    }
                    finally
                    {
                        advancing = false;
                    }

                    if (IsActive)
                        yield return wait;
                }
            }
            finally
            {
                DisposeCoroutine();
            }
        }

        void DisposeCoroutine()
        {
            if (disposed)
                return;

            disposed = true;
            try
            {
                coroutine.Dispose();
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }
    }

    /// <summary>起動済みの MEC コルーチンを登録し、シーケンスの終了時に停止します。</summary>
    public CoroutineHandle Track(CoroutineHandle handle)
    {
        OnEnd(() => Timing.KillCoroutines(handle));
        return handle;
    }

    /// <summary>後始末を登録します。終了時に登録の逆順で呼ばれ、終了済みなら即座に実行されます。</summary>
    public void OnEnd(Action cleanup)
    {
        if (cleanup is null)
            throw new ArgumentNullException(nameof(cleanup));

        if (IsActive)
            cleanups.Add(cleanup);
        else
            SequenceManager.InvokeCleanup(cleanup);
    }

    internal void Close(SequenceEndReason reason)
    {
        EndReason = reason;
        for (int i = cleanups.Count - 1; i >= 0; i--)
            SequenceManager.InvokeCleanup(cleanups[i]);

        cleanups.Clear();

        SequenceManager.InvokeCleanup(() => Sequence.End(reason));
    }

    private void Fail(Exception exception)
    {
        SequenceManager.Report(exception);
        SequenceManager.TryEnd(this, SequenceEndReason.Failed);
    }
}
