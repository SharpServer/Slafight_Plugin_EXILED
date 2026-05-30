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

    private readonly int _timerY = HintCoordinateConverter.FromRueiY(910);
    private readonly int _stateY = HintCoordinateConverter.FromRueiY(900);
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

    private void OnVerified(VerifiedEventArgs ev)
    {
        if (ev?.Player == null)
            return;

        Timing.CallDelayed(0.5f, () => EnsureFor(ev.Player));
    }

    private void OnChangingRole(ChangingRoleEventArgs ev)
    {
        if (ev?.Player == null)
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
        foreach (var player in Player.List.ToList())
            EnsureFor(player);
    }

    private void EnsureFor(Player player)
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
        SetText(display, TimerHintId, BuildTimerText());
        SetText(display, StateHintId, BuildStateText());
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
        if (hint != null)
            hint.Text = text;
    }

    private static string BuildTimerText()
    {
        TimeSpan ntfTime = GetNextFactionWaveTime(Faction.FoundationStaff);
        TimeSpan chaosTime = GetNextFactionWaveTime(Faction.FoundationEnemy);

        bool isEqual = ntfTime > TimeSpan.Zero && chaosTime > TimeSpan.Zero && ntfTime == chaosTime;
        bool isNtfNext = ntfTime > TimeSpan.Zero && (ntfTime < chaosTime || chaosTime == TimeSpan.Zero) && !isEqual;
        bool isChaosNext = chaosTime > TimeSpan.Zero && (chaosTime < ntfTime || ntfTime == TimeSpan.Zero) && !isEqual;

        string ntf = FormatFactionTime(ntfTime, FoundationColor, isNtfNext || isEqual);
        string chaos = FormatFactionTime(chaosTime, ChaosColor, isChaosNext || isEqual);
        return $"<align=center>{ntf}<space=450>{chaos}</align>";
    }

    private static string BuildStateText()
    {
        return TranslateWaveQueueState(Respawn.CurrentState);
    }

    private static string FormatFactionTime(TimeSpan time, string color, bool highlight)
    {
        string text = (highlight ? "► " : string.Empty) + FormatTimeSpanToJapanese(time);
        return highlight ? $"<color={color}>{text}</color>" : text;
    }

    private static string FormatTimeSpanToJapanese(TimeSpan timeSpan)
    {
        if (timeSpan == TimeSpan.Zero)
            return "利用不可";

        if (timeSpan < TimeSpan.Zero)
            return "まもなく到着";

        return $"{(int)timeSpan.TotalMinutes}分{timeSpan.Seconds}秒";
    }

    private static TimeSpan GetNextFactionWaveTime(Faction faction)
    {
        TimeSpan shortestTime = TimeSpan.MaxValue;
        bool waveFound = false;

        foreach (var wave in WaveManager.Waves)
        {
            if (wave is not TimeBasedWave timeBasedWave || timeBasedWave.Timer.IsPaused)
                continue;

            bool isCorrectFaction = faction switch
            {
                Faction.FoundationStaff => wave is NtfSpawnWave or NtfMiniWave,
                Faction.FoundationEnemy => wave is ChaosSpawnWave or ChaosMiniWave,
                _ => false,
            };

            if (!isCorrectFaction)
                continue;

            var timeLeft = TimeSpan.FromSeconds(timeBasedWave.Timer.TimeLeft);
            if (timeLeft < shortestTime)
            {
                shortestTime = timeLeft;
                waveFound = true;
            }
        }

        return waveFound ? shortestTime : TimeSpan.Zero;
    }

    private static string TranslateWaveQueueState(WaveQueueState waveQueueState)
    {
        TimeSpan ntfTime = GetNextFactionWaveTime(Faction.FoundationStaff);
        TimeSpan chaosTime = GetNextFactionWaveTime(Faction.FoundationEnemy);

        bool isEqual = ntfTime > TimeSpan.Zero && chaosTime > TimeSpan.Zero && ntfTime == chaosTime;
        bool isNtfNext = ntfTime > TimeSpan.Zero && (ntfTime < chaosTime || chaosTime == TimeSpan.Zero);
        bool isChaosNext = chaosTime > TimeSpan.Zero && (chaosTime < ntfTime || ntfTime == TimeSpan.Zero);

        SpawnableFaction spawnableFaction = Respawn.NextKnownSpawnableFaction;
        string factionName = spawnableFaction is SpawnableFaction.ChaosWave or SpawnableFaction.ChaosMiniWave
            ? $"<color={ChaosColor}>カオス</color>"
            : $"<color={FoundationColor}>九尾狐</color>";

        return waveQueueState switch
        {
            WaveQueueState.Idle when isEqual => $"現在<color={BalanceColor}>均衡中</color>",
            WaveQueueState.Idle when isChaosNext => $"現在<color={ChaosColor}>カオス</color>が優勢",
            WaveQueueState.Idle when isNtfNext => $"現在<color={FoundationColor}>九尾狐</color>が優勢",
            WaveQueueState.WaveSelected => $"{factionName}陣営が準備開始",
            WaveQueueState.WaveSpawning => $"{factionName}がまもなく到着",
            WaveQueueState.WaveSpawned => $"{factionName}が到着",
            _ => "スポーン不可",
        };
    }
}
