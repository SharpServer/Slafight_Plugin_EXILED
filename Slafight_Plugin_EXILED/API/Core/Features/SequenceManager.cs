using System;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Server;
using ServerHandlers = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>ラウンド内で同時に一つだけシーケンスを実行します。</summary>
public static class SequenceManager
{
    private static bool accepting;
    private static bool transitioning;

    public static SequenceRun CurrentRun { get; private set; }
    public static SequenceBase CurrentSequence => CurrentRun?.Sequence;

    public static bool CancelCurrent() => CurrentRun?.Cancel() ?? false;

    internal static bool CanStart(SequenceBase sequence)
    {
        if (!accepting || transitioning)
            return false;

        transitioning = true;
        try
        {
            return AllowsReplacement(sequence) && sequence.CheckTriggerConditions() &&
                   AllowsReplacement(sequence) && accepting;
        }
        catch (Exception exception)
        {
            Report(exception);
            return false;
        }
        finally
        {
            transitioning = false;
        }
    }

    private static bool AllowsReplacement(SequenceBase sequence) =>
        CurrentSequence is not { } current ||
        (!ReferenceEquals(current, sequence) && current.AllowOverride && sequence.Priority > current.Priority);

    internal static bool TryStart(SequenceBase sequence, bool force = false)
    {
        if (!accepting || transitioning || (!force && !CanStart(sequence)))
            return false;

        transitioning = true;
        try
        {
            TryEnd(CurrentRun, SequenceEndReason.Overridden);
            if (!accepting)
                return false;

            SequenceRun run = new(sequence);
            CurrentRun = run;
            try
            {
                sequence.Start(run);
                return run.EndReason != SequenceEndReason.Failed;
            }
            catch (Exception exception)
            {
                Report(exception);
                TryEnd(run, SequenceEndReason.Failed);
                return false;
            }
        }
        finally
        {
            transitioning = false;
        }
    }

    internal static bool TryEnd(SequenceRun run, SequenceEndReason reason)
    {
        if (run is null || !ReferenceEquals(CurrentRun, run))
            return false;

        bool wasTransitioning = transitioning;
        transitioning = true;
        CurrentRun = null;
        try
        {
            run.Close(reason);
            return true;
        }
        finally
        {
            transitioning = wasTransitioning;
        }
    }

    internal static void Initialize()
    {
        accepting = Round.IsStarted && !Round.IsEnded;
        ServerHandlers.RoundStarted += OnRoundStarted;
        ServerHandlers.RoundEnded += OnRoundEnded;
        ServerHandlers.RestartingRound += OnRoundClosed;
    }

    internal static void Shutdown()
    {
        ServerHandlers.RoundStarted -= OnRoundStarted;
        ServerHandlers.RoundEnded -= OnRoundEnded;
        ServerHandlers.RestartingRound -= OnRoundClosed;
        accepting = false;
        TryEnd(CurrentRun, SequenceEndReason.PluginDisabled);
    }

    private static void OnRoundStarted() => accepting = true;
    private static void OnRoundEnded(RoundEndedEventArgs _) => OnRoundClosed();

    private static void OnRoundClosed()
    {
        accepting = false;
        TryEnd(CurrentRun, SequenceEndReason.RoundEnded);
    }

    internal static void InvokeCleanup(Action cleanup)
    {
        try
        {
            cleanup();
        }
        catch (Exception exception)
        {
            Report(exception);
        }
    }

    internal static void Report(Exception exception) =>
        Log.Error($"[Slafight] Sequence の処理で例外が発生しました: {exception}");
}
