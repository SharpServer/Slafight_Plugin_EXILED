#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Utilities;
using MEC;
using PlayerRoles;
using Respawning;
using Respawning.Waves;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Interface;
using Hint = HintServiceMeow.Core.Models.Hints.Hint;

namespace Slafight_Plugin_EXILED.Hints;

public sealed class RespawnTimerHints : IBootstrapHandler, IDisposable
{
    private const string TimerHintId = "RespawnTimer_Time";
    private const string StateHintId = "RespawnTimer_State";
    private const string FoundationColor = "#00b7eb";
    private const string ChaosColor = "#228b22";
    private const string BalanceColor = "#FFD700";
    private static RespawnTimerHints? _instance;

    private readonly int _timerY = 90;
    private readonly int _stateY = 110;
    private CoroutineHandle _loop;
    private bool _disposed;

    public static void Register()
    {
        Unregister();
        _instance = new RespawnTimerHints();
    }

    public static void Unregister()
    {
        _instance?.Dispose();
        _instance = null;
    }

    private RespawnTimerHints()
    {
        Exiled.Events.Handlers.Player.Verified += OnVerified;
        Exiled.Events.Handlers.Player.ChangingRole += OnChangingRole;
        Exiled.Events.Handlers.Server.RoundStarted += EnsureAll;
        Exiled.Events.Handlers.Server.RestartingRound += ClearAll;
        _loop = Timing.RunCoroutine(UpdateLoop());
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Exiled.Events.Handlers.Player.Verified -= OnVerified;
        Exiled.Events.Handlers.Player.ChangingRole -= OnChangingRole;
        Exiled.Events.Handlers.Server.RoundStarted -= EnsureAll;
        Exiled.Events.Handlers.Server.RestartingRound -= ClearAll;

        if (_loop.IsRunning)
            Timing.KillCoroutines(_loop);

        ClearAll();
        GC.SuppressFinalize(this);
    }

    private void OnVerified(VerifiedEventArgs? ev)
    {
        if (ev?.Player == null)
            return;

        Timing.CallDelayed(0.5f, () => EnsureFor(ev.Player));
    }

    private void OnChangingRole(ChangingRoleEventArgs? ev)
    {
        if (ev?.Player == null)
            return;
        if (!ev.IsAllowed)
            return;

        Timing.CallDelayed(0.5f, () => EnsureFor(ev.Player));
    }

    private IEnumerator<float> UpdateLoop()
    {
        yield return Timing.WaitForSeconds(0.5f);
        for (;;)
        {
            EnsureAll();
            yield return Timing.WaitForSeconds(0.5f);
        }
    }

    private void EnsureAll()
    {
        var text = BuildTexts();
        foreach (var player in Player.List.ToList())
            EnsureFor(player, text.timer, text.state);
    }

    private void EnsureFor(Player player)
    {
        var text = BuildTexts();
        EnsureFor(player, text.timer, text.state);
    }

    private void EnsureFor(Player player, string timerText, string stateText)
    {
        if (!IsPlayerValid(player))
            return;

        var display = TryGetDisplay(player);
        if (display == null)
            return;

        if (!ShouldShow(player))
        {
            SetText(display, TimerHintId, string.Empty);
            SetText(display, StateHintId, string.Empty);
            return;
        }

        EnsureHint(display, TimerHintId, _timerY);
        EnsureHint(display, StateHintId, _stateY);
        SetText(display, TimerHintId, timerText);
        SetText(display, StateHintId, stateText);
    }

    private void ClearAll()
    {
        foreach (var player in Player.List.ToList())
        {
            if (!IsPlayerValid(player))
                continue;

            var display = TryGetDisplay(player);
            if (display == null)
                continue;

            SetText(display, TimerHintId, string.Empty);
            SetText(display, StateHintId, string.Empty);
        }
    }

    private static bool ShouldShow(Player player)
    {
        return player.Role.Type is RoleTypeId.Spectator or RoleTypeId.Overwatch;
    }

    private static bool IsPlayerValid(Player? player)
    {
        try
        {
            return player != null && player.IsConnected && player.ReferenceHub != null;
        }
        catch
        {
            return false;
        }
    }

    private static PlayerDisplay? TryGetDisplay(Player player)
    {
        try
        {
            return PlayerDisplay.Get(player.ReferenceHub);
        }
        catch
        {
            return null;
        }
    }

    private static void EnsureHint(PlayerDisplay display, string id, int y)
    {
        if (display.GetHint(id) != null)
            return;

        display.AddHint(new Hint
        {
            Id = id,
            Text = string.Empty,
            Alignment = HintAlignment.Center,
            SyncSpeed = HintSyncSpeed.Fastest,
            FontSize = 24,
            XCoordinate = 0,
            YCoordinate = y,
        });
    }

    private static void SetText(PlayerDisplay display, string id, string text)
    {
        var hint = display.GetHint(id);
        if (hint != null && hint.Text != text)
            hint.Text = text;
    }

    private static (string timer, string state) BuildTexts()
    {
        var waves = GetNextFactionWaves();
        var ntfWave = waves.ntf;
        var chaosWave = waves.chaos;

        var ntfTime = GetWaveTimeLeft(ntfWave);
        var chaosTime = GetWaveTimeLeft(chaosWave);

        bool isEqual = ntfTime > TimeSpan.Zero && chaosTime > TimeSpan.Zero && ntfTime == chaosTime;
        bool isNtfNext = ntfTime > TimeSpan.Zero && (ntfTime < chaosTime || chaosTime == TimeSpan.Zero) && !isEqual;
        bool isChaosNext = chaosTime > TimeSpan.Zero && (chaosTime < ntfTime || ntfTime == TimeSpan.Zero) && !isEqual;

        string ntf = FormatFactionTime(ntfWave, ntfTime, FoundationColor, isNtfNext || isEqual);
        string chaos = FormatFactionTime(chaosWave, chaosTime, ChaosColor, isChaosNext || isEqual);
        string timer = $"<align=center>{ntf}<space=450>{chaos}</align>";
        string state = TranslateWaveQueueState(Respawn.CurrentState, ntfTime, chaosTime);
        return (timer, state);
    }

    private static string FormatFactionTime(TimeBasedWave? wave, TimeSpan time, string color, bool highlight)
    {
        string text = (highlight ? "► " : string.Empty) + FormatTimeSpanToJapanese(wave, time);
        return highlight ? $"<color={color}>{text}</color>" : text;
    }

    private static string FormatTimeSpanToJapanese(TimeBasedWave? wave, TimeSpan timeSpan)
    {
        if (wave == null)
            return "一時停止中";

        // IsForcefullyPaused のみを使用（元のコードと同じ条件）
        if (wave.Timer.IsForcefullyPaused)
            return "一時停止中";

        if (timeSpan == TimeSpan.Zero)
            return "利用不可";

        if (timeSpan < TimeSpan.Zero)
            return "まもなく到着";

        return $"{(int)timeSpan.TotalMinutes}分{timeSpan.Seconds}秒";
    }

    private static (TimeBasedWave? ntf, TimeBasedWave? chaos) GetNextFactionWaves()
    {
        return (
            GetNextFactionWave(wave => wave is NtfSpawnWave or NtfMiniWave),
            GetNextFactionWave(wave => wave is ChaosSpawnWave or ChaosMiniWave));
    }

    private static TimeBasedWave? GetNextFactionWave(Func<SpawnableWaveBase, bool> belongsToFaction)
    {
        TimeBasedWave? selected = null;
        TimeBasedWave? ready = null;
        TimeBasedWave? moving = null;
        TimeBasedWave? paused = null;
        float shortestMoving = float.MaxValue;
        float shortestPaused = float.MaxValue;

        foreach (var wave in WaveManager.Waves)
        {
            if (!belongsToFaction(wave) || wave is not TimeBasedWave timeBasedWave)
                continue;

            if (IsSelectedWave(timeBasedWave))
                selected = timeBasedWave;

            if (!CanDisplayAsActiveTimer(timeBasedWave))
                continue;

            var timeLeft = timeBasedWave.Timer.TimeLeft;

            if (ready == null && IsSpawnableNow(timeBasedWave))
            {
                ready = timeBasedWave;
                continue;
            }

            // Match WaveTimer.Update: no-token mini waves can count down to 30s and then freeze.
            if (IsTimerAdvancing(timeBasedWave))
            {
                if (timeLeft < shortestMoving)
                {
                    shortestMoving = timeLeft;
                    moving = timeBasedWave;
                }
            }
            else if (timeLeft < shortestPaused)
            {
                shortestPaused = timeLeft;
                paused = timeBasedWave;
            }
        }

        return selected ?? ready ?? moving ?? paused;
    }

    private static bool IsSelectedWave(TimeBasedWave wave)
    {
        return Respawn.CurrentState != WaveQueueState.Idle && ReferenceEquals(WaveManager._nextWave, wave);
    }

    private static bool CanDisplayAsActiveTimer(TimeBasedWave wave)
    {
        var timer = wave.Timer;
        return timer != null &&
               wave.Configuration.IsEnabled &&
               !timer.IsForcefullyPaused &&
               !timer.IsOutOfRespawns;
    }

    private static bool IsSpawnableNow(TimeBasedWave wave)
    {
        return wave.IsReadyToSpawn && !wave.Timer.IsPaused;
    }

    private static bool IsTimerAdvancing(TimeBasedWave wave)
    {
        var timer = wave.Timer;
        return !timer.IsForcefullyPaused &&
               !timer.IsOutOfRespawns &&
               (!timer.IsPaused || timer.TimeLeft > WaveTimer.CountdownPauseThresholdSeconds) &&
               RoundSummary.RoundInProgress();
    }

    private static TimeSpan GetWaveTimeLeft(TimeBasedWave? wave)
    {
        if (wave == null)
            return TimeSpan.Zero;

        return TimeSpan.FromSeconds(wave.Timer.TimeLeft);
    }

    private static string TranslateWaveQueueState(WaveQueueState waveQueueState, TimeSpan ntfTime, TimeSpan chaosTime)
    {
        bool isEqual = ntfTime > TimeSpan.Zero && chaosTime > TimeSpan.Zero && ntfTime == chaosTime;
        bool isNtfNext = ntfTime > TimeSpan.Zero && (ntfTime < chaosTime || chaosTime == TimeSpan.Zero);
        bool isChaosNext = chaosTime > TimeSpan.Zero && (chaosTime < ntfTime || ntfTime == TimeSpan.Zero);

        SpawnableFaction spawnableFaction = Respawn.NextKnownSpawnableFaction;
        string factionName = spawnableFaction is SpawnableFaction.ChaosWave or SpawnableFaction.ChaosMiniWave
            ? $"<color={ChaosColor}>要注意団体</color>"
            : $"<color={FoundationColor}>財団部隊</color>";

        return waveQueueState switch
        {
            WaveQueueState.Idle when isEqual => $"現在<color={BalanceColor}>均衡中</color>",
            WaveQueueState.Idle when isChaosNext => $"現在<color={ChaosColor}>要注意団体</color>が優勢",
            WaveQueueState.Idle when isNtfNext => $"現在<color={FoundationColor}>財団部隊</color>が優勢",
            WaveQueueState.WaveSelected => $"{factionName}陣営が準備開始",
            WaveQueueState.WaveSpawning => $"{factionName}がまもなく到着",
            WaveQueueState.WaveSpawned => $"{factionName}が到着",
            _ => "スポーン不可",
        };
    }
}
