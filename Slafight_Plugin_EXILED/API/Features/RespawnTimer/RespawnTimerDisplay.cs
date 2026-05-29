using System;
using System.Text;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using Respawning;
using Respawning.Waves;
using Respawning.Waves.Generic;
using RueI.API;
using RueI.API.Elements;
using Slafight_Plugin_EXILED.API.Interface;

namespace Slafight_Plugin_EXILED.API.Features.RespawnTimer;

public sealed class RespawnTimerDisplay : IBootstrapHandler, IDisposable
{
    private static readonly Tag SpecTimerTag = new("SpecTimer");
    private static readonly Tag SpawnSituationTag = new("SpawnSituation");

    public static DynamicElement SpecTimer { get; } = new(910, GetTimers)
    {
        UpdateInterval = TimeSpan.FromSeconds(1)
    };

    public static DynamicElement SpecSpawn { get; } = new(900, GetRespawnSituation)
    {
        UpdateInterval = TimeSpan.FromTicks(500)
    };

    public static RespawnTimerDisplay? Instance { get; private set; }

    public static void Register()
    {
        Unregister();
        Instance = new RespawnTimerDisplay();
    }

    public static void Unregister()
    {
        Instance?.Dispose();
        Instance = null;
    }

    private bool _disposed;

    private RespawnTimerDisplay()
    {
        Exiled.Events.Handlers.Player.Verified += OnVerified;
        Exiled.Events.Handlers.Player.ChangingRole += OnChangingRole;
        Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;
        Exiled.Events.Handlers.Server.RestartingRound += OnRestartingRound;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Exiled.Events.Handlers.Player.Verified -= OnVerified;
        Exiled.Events.Handlers.Player.ChangingRole -= OnChangingRole;
        Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;
        Exiled.Events.Handlers.Server.RestartingRound -= OnRestartingRound;

        foreach (var player in Player.List)
            RemoveElements(player);

        GC.SuppressFinalize(this);
    }

    private static void OnVerified(VerifiedEventArgs ev)
    {
        if (ev?.Player == null)
            return;

        ApplyForRole(ev.Player, ev.Player.Role.Type);
    }

    private static void OnChangingRole(ChangingRoleEventArgs ev)
    {
        if (ev?.Player == null)
            return;

        Timing.CallDelayed(0.1f, () => ApplyForRole(ev.Player, ev.NewRole));
    }

    private static void OnRoundStarted()
    {
        Timing.CallDelayed(0.5f, () =>
        {
            foreach (var player in Player.List)
                ApplyForRole(player, player.Role.Type);
        });
    }

    private static void OnRestartingRound()
    {
        foreach (var player in Player.List)
            RemoveElements(player);
    }

    private static void ApplyForRole(Player player, RoleTypeId role)
    {
        if (!IsValid(player))
            return;

        RemoveElements(player);

        if (role != RoleTypeId.Spectator && role != RoleTypeId.Overwatch)
            return;

        try
        {
            var display = RueDisplay.Get(player.ReferenceHub);
            display.Show(SpecTimerTag, SpecTimer);
            display.Show(SpawnSituationTag, SpecSpawn);
        }
        catch (Exception ex)
        {
            Log.Debug($"[RespawnTimerDisplay] show failed for {player.Nickname}: {ex.Message}");
        }
    }

    private static void RemoveElements(Player? player)
    {
        if (!IsValid(player))
            return;

        try
        {
            var display = RueDisplay.Get(player!.ReferenceHub);
            display.Remove(SpecTimerTag);
            display.Remove(SpawnSituationTag);
        }
        catch
        {
            // The display may not exist yet, or the tags may already be gone.
        }
    }

    private static bool IsValid(Player? player)
    {
        try
        {
            return player != null &&
                   player.IsConnected &&
                   !player.IsHost &&
                   !player.IsNPC &&
                   player.ReferenceHub != null &&
                   player.ReferenceHub.connectionToClient != null;
        }
        catch
        {
            return false;
        }
    }

    private static TimeSpan GetNextFactionWaveTime(Faction faction)
    {
        TimeSpan shortestTime = TimeSpan.MaxValue;
        bool waveFound = false;

        foreach (var wave in WaveManager.Waves)
        {
            if (wave is not TimeBasedWave timeBasedWave || timeBasedWave.Timer.IsPaused)
                continue;

            bool isCorrectFaction = false;
            if (faction == Faction.FoundationStaff)
            {
                if (wave is NtfSpawnWave || wave is NtfMiniWave)
                    isCorrectFaction = true;
            }
            else if (faction == Faction.FoundationEnemy)
            {
                if (wave is ChaosSpawnWave || wave is ChaosMiniWave)
                    isCorrectFaction = true;
            }

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

    private static string FormatTimeSpanToJapanese(TimeSpan timeSpan)
    {
        if (timeSpan == TimeSpan.Zero)
            return "利用不可";

        if (timeSpan < TimeSpan.Zero)
            return "まもなく到着";

        return $"{(int)timeSpan.TotalMinutes}分{timeSpan.Seconds}秒";
    }

    private static string GetTimers(ReferenceHub core)
    {
        TimeSpan ntfTime = GetNextFactionWaveTime(Faction.FoundationStaff);
        TimeSpan chaosTime = GetNextFactionWaveTime(Faction.FoundationEnemy);

        bool isEqual = ntfTime > TimeSpan.Zero && chaosTime > TimeSpan.Zero && ntfTime == chaosTime;
        bool isNtfNext = ntfTime > TimeSpan.Zero && (ntfTime < chaosTime || chaosTime == TimeSpan.Zero) && !isEqual;
        bool isChaosNext = chaosTime > TimeSpan.Zero && (chaosTime < ntfTime || ntfTime == TimeSpan.Zero) && !isEqual;

        var builder = new StringBuilder()
            .Append("<align=center>");

        if (isNtfNext || isEqual)
            builder.Append($"<color={RoleExtensions.GetColor(RoleTypeId.NtfCaptain).ToHex()}>");

        builder.Append((isNtfNext || isEqual) ? "► " : string.Empty);
        builder.Append(FormatTimeSpanToJapanese(ntfTime));

        if (isNtfNext || isEqual)
            builder.Append("</color>");

        builder.Append("<space=450>");

        if (isChaosNext || isEqual)
            builder.Append($"<color={RoleExtensions.GetColor(RoleTypeId.ChaosRifleman).ToHex()}>");

        builder.Append((isChaosNext || isEqual) ? "► " : string.Empty);
        builder.Append(FormatTimeSpanToJapanese(chaosTime));

        if (isChaosNext || isEqual)
            builder.Append("</color>");

        builder.Append("</align>");
        return builder.ToString();
    }

    private static string GetRespawnSituation(ReferenceHub hub)
    {
        var state = TranslateWaveQueueState(Respawn.CurrentState);
        return state + Environment.NewLine;
    }

    private static string TranslateWaveQueueState(WaveQueueState waveQueueState)
    {
        TimeSpan ntfTime = GetNextFactionWaveTime(Faction.FoundationStaff);
        TimeSpan chaosTime = GetNextFactionWaveTime(Faction.FoundationEnemy);

        bool isEqual = ntfTime > TimeSpan.Zero && chaosTime > TimeSpan.Zero && ntfTime == chaosTime;
        bool isNtfNext = ntfTime > TimeSpan.Zero && (ntfTime < chaosTime || chaosTime == TimeSpan.Zero);
        bool isChaosNext = chaosTime > TimeSpan.Zero && (chaosTime < ntfTime || ntfTime == TimeSpan.Zero);

        SpawnableFaction spawnableFaction = Respawn.NextKnownSpawnableFaction;
        string factionName = (spawnableFaction == SpawnableFaction.ChaosWave ||
                              spawnableFaction == SpawnableFaction.ChaosMiniWave)
            ? $"<color={RoleExtensions.GetColor(RoleTypeId.ChaosConscript).ToHex()}>カオス</color>"
            : $"<color={RoleExtensions.GetColor(RoleTypeId.NtfCaptain).ToHex()}>九尾狐</color>";

        switch (waveQueueState)
        {
            case WaveQueueState.Idle:
                if (isEqual)
                    return "現在<color=#FFD700>均衡中</color>";
                if (isChaosNext)
                    return $"現在<color={RoleExtensions.GetColor(RoleTypeId.ChaosConscript).ToHex()}>カオス</color>が優勢";
                if (isNtfNext)
                    return $"現在<color={RoleExtensions.GetColor(RoleTypeId.NtfCaptain).ToHex()}>九尾狐</color>が優勢";

                break;
            case WaveQueueState.WaveSelected:
                return $"{factionName}陣営が準備開始";
            case WaveQueueState.WaveSpawning:
                return $"{factionName}がまもなく到着";
            case WaveQueueState.WaveSpawned:
                return $"{factionName}が到着";
            default:
                return "スポーン不可";
        }

        return "スポーン不可";
    }
}
