using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using MEC;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.Common;
using Slafight_Plugin_EXILED.API.Features.Teams.Profiles;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.API.Features.RoundVictory.Core;

/// <summary>
/// 独自ラウンド終了処理に渡す特殊理由をまとめます。
/// </summary>
public static class RoundEndReasons
{
    /// <summary>
    /// 雪の戦士達の勝利。
    /// </summary>
    public const string SnowWarrierWin = "SW_WIN";

    /// <summary>
    /// お菓子の戦士達の勝利。
    /// </summary>
    public const string CandyWarrierWin = "CANDY_WIN";

    /// <summary>
    /// FacilityTermination で人類側が勝利した場合。
    /// </summary>
    public const string SavedHumanity = "SavedHumanity";

    /// <summary>
    /// FacilityTermination で正常性側が勝利した場合。
    /// </summary>
    public const string NoHumanityAllowed = "NoHumanityAllowed";
}

/// <summary>
/// vanilla の勝利計算へ渡すために操作するラウンド統計値の種類。
/// </summary>
public enum RoundEndScoreMode
{
    /// <summary>
    /// ラウンド統計値を操作しません。
    /// </summary>
    None,

    /// <summary>
    /// <see cref="Round.KillsByScp"/> を操作します。
    /// </summary>
    KillsByScp,

    /// <summary>
    /// <see cref="Round.EscapedDClasses"/> を操作します。
    /// </summary>
    EscapedDClasses,

    /// <summary>
    /// <see cref="Round.EscapedScientists"/> を操作します。
    /// </summary>
    EscapedScientists,
}

/// <summary>
/// ラウンド終了時に実行する処理を表します。
/// </summary>
public sealed class RoundEndDefinition
{
    /// <summary>
    /// ラウンド終了定義を作成します。
    /// </summary>
    /// <param name="debugName">ログや調査用の識別名。</param>
    /// <param name="winnerTeam">勝利チーム。</param>
    /// <param name="specificReason">特殊理由。null の場合はチームの標準終了定義として扱います。</param>
    /// <param name="scoreMode">操作するラウンド統計値。</param>
    /// <param name="scoreValue">統計値へ代入する値。</param>
    /// <param name="useVanillaEndRound">true の場合、統計値の操作後に <see cref="Round.EndRound(bool)"/> を呼びます。</param>
    /// <param name="victoryHint">独自勝利として全員に表示するヒント。null の場合は表示しません。</param>
    /// <param name="hintDuration">勝利ヒントの表示秒数。</param>
    /// <param name="overrideIntercom">勝利表示中にインターコム override を有効化するか。</param>
    /// <param name="restartDelay">独自勝利後に再起動するまでの秒数。null の場合は自動再起動しません。</param>
    /// <param name="beforeEnd">終了処理の直前に実行する追加処理。</param>
    public RoundEndDefinition(
        string debugName,
        CTeam winnerTeam,
        string? specificReason = null,
        RoundEndScoreMode scoreMode = RoundEndScoreMode.None,
        int scoreValue = 999,
        bool useVanillaEndRound = false,
        string? victoryHint = null,
        float hintDuration = 555f,
        bool overrideIntercom = true,
        float? restartDelay = 10f,
        Action? beforeEnd = null)
    {
        DebugName = debugName;
        WinnerTeam = winnerTeam;
        SpecificReason = specificReason;
        ScoreMode = scoreMode;
        ScoreValue = scoreValue;
        UseVanillaEndRound = useVanillaEndRound;
        VictoryHint = victoryHint;
        HintDuration = hintDuration;
        OverrideIntercom = overrideIntercom;
        RestartDelay = restartDelay;
        BeforeEnd = beforeEnd;
    }

    /// <summary>
    /// ログや調査用の識別名。
    /// </summary>
    public string DebugName { get; }

    /// <summary>
    /// 勝利チーム。
    /// </summary>
    public CTeam WinnerTeam { get; }

    /// <summary>
    /// 特殊理由。null の場合はチームの標準終了定義として扱います。
    /// </summary>
    public string? SpecificReason { get; }

    /// <summary>
    /// 操作するラウンド統計値。
    /// </summary>
    public RoundEndScoreMode ScoreMode { get; }

    /// <summary>
    /// 統計値へ代入する値。
    /// </summary>
    public int ScoreValue { get; }

    /// <summary>
    /// true の場合、統計値の操作後に vanilla の <see cref="Round.EndRound(bool)"/> を呼びます。
    /// </summary>
    public bool UseVanillaEndRound { get; }

    /// <summary>
    /// 独自勝利として全員に表示するヒント。
    /// </summary>
    public string? VictoryHint { get; }

    /// <summary>
    /// 勝利ヒントの表示秒数。
    /// </summary>
    public float HintDuration { get; }

    /// <summary>
    /// 勝利表示中にインターコム override を有効化するか。
    /// </summary>
    public bool OverrideIntercom { get; }

    /// <summary>
    /// 独自勝利後に再起動するまでの秒数。null の場合は自動再起動しません。
    /// </summary>
    public float? RestartDelay { get; }

    /// <summary>
    /// 終了処理の直前に実行する追加処理。
    /// </summary>
    public Action? BeforeEnd { get; }

    public static RoundEndDefinition Vanilla(
        string debugName,
        CTeam winnerTeam,
        RoundEndScoreMode scoreMode) =>
        new(debugName, winnerTeam, scoreMode: scoreMode, useVanillaEndRound: true, restartDelay: null);

    public static RoundEndDefinition Custom(
        string debugName,
        CTeam winnerTeam,
        string victoryHint,
        RoundEndScoreMode scoreMode = RoundEndScoreMode.None,
        int scoreValue = 999,
        string? specificReason = null,
        Action? beforeEnd = null) =>
        new(debugName, winnerTeam, specificReason, scoreMode, scoreValue, victoryHint: victoryHint, beforeEnd: beforeEnd);
}

internal readonly struct RoundEndDefinitionKey : IEquatable<RoundEndDefinitionKey>
{
    public RoundEndDefinitionKey(CTeam team, string? specificReason)
    {
        Team = team;
        SpecificReason = specificReason;
    }

    public CTeam Team { get; }
    public string? SpecificReason { get; }

    public bool Equals(RoundEndDefinitionKey other) =>
        Team == other.Team &&
        string.Equals(SpecificReason, other.SpecificReason, StringComparison.Ordinal);

    public override bool Equals(object obj) =>
        obj is RoundEndDefinitionKey other && Equals(other);

    public override int GetHashCode() =>
        ((int)Team * 397) ^ (SpecificReason?.GetHashCode() ?? 0);
}

/// <summary>
/// ラウンド終了定義を提供します。
/// </summary>
public interface IRoundEndDefinitionSource
{
    IEnumerable<RoundEndDefinition> GetTeamDefaults();
    IEnumerable<RoundEndDefinition> GetSpecificDefinitions();
}

public abstract class RoundEndDefinitionSource : IRoundEndDefinitionSource
{
    public virtual IEnumerable<RoundEndDefinition> GetTeamDefaults()
    {
        yield break;
    }

    public virtual IEnumerable<RoundEndDefinition> GetSpecificDefinitions()
    {
        yield break;
    }
}

public static class RoundEndDefinitions
{
    private static readonly Lazy<IReadOnlyDictionary<RoundEndDefinitionKey, RoundEndDefinition>> SpecificDefinitions =
        new(BuildSpecificDefinitions);

    private static readonly Lazy<IReadOnlyDictionary<CTeam, RoundEndDefinition>> TeamDefaults =
        new(BuildTeamDefaults);

    private static readonly RoundEndDefinition UnknownDefinition = RoundEndDefinition.Custom(
        "UnknownTeamWin",
        CTeam.Null,
        "<b><size=80><color=#ffffff>UNKNOWN TEAM</color>の勝利</size></b>");

    /// <summary>
    /// チームと特殊理由からラウンド終了定義を取得します。
    /// </summary>
    /// <param name="winnerTeam">勝利チーム。</param>
    /// <param name="specificReason">特殊理由。</param>
    /// <returns>ラウンド終了定義。</returns>
    public static RoundEndDefinition Get(CTeam winnerTeam, string? specificReason)
    {
        return TryGet(winnerTeam, specificReason, out var definition)
            ? definition
            : UnknownDefinition;
    }

    public static bool TryGet(CTeam winnerTeam, string? specificReason, out RoundEndDefinition definition)
    {
        if (!string.IsNullOrEmpty(specificReason) &&
            SpecificDefinitions.Value.TryGetValue(Key(winnerTeam, specificReason), out var specificDefinition))
        {
            definition = specificDefinition;
            return true;
        }

        if (TeamDefaults.Value.TryGetValue(winnerTeam, out var teamDefinition))
        {
            definition = teamDefinition;
            return true;
        }

        definition = UnknownDefinition;
        return false;
    }

    public static RoundEndDefinition Get(CTeamGroup winnerGroup, string? specificReason)
    {
        var victory = CTeamProfileRegistry.GetVictory(winnerGroup);
        return Get(victory.WinnerTeam, specificReason ?? victory.SpecificReason);
    }

    public static bool TryGet(CTeamGroup winnerGroup, string? specificReason, out RoundEndDefinition definition)
    {
        var victory = CTeamProfileRegistry.GetVictory(winnerGroup);
        return TryGet(victory.WinnerTeam, specificReason ?? victory.SpecificReason, out definition);
    }

    private static RoundEndDefinitionKey Key(CTeam team, string? specificReason) =>
        new(team, specificReason);

    private static IReadOnlyDictionary<RoundEndDefinitionKey, RoundEndDefinition> BuildSpecificDefinitions()
    {
        var definitions = DefinitionSourceLoader
            .CreateInstances<IRoundEndDefinitionSource>()
            .SelectMany(source => source.GetSpecificDefinitions())
            .ToList();

        var duplicate = definitions
            .GroupBy(definition => Key(definition.WinnerTeam, definition.SpecificReason))
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate != null)
            throw new InvalidOperationException(
                $"Duplicate round end specific definition: {duplicate.Key.Team}/{duplicate.Key.SpecificReason}");

        return definitions.ToDictionary(definition => Key(definition.WinnerTeam, definition.SpecificReason));
    }

    private static IReadOnlyDictionary<CTeam, RoundEndDefinition> BuildTeamDefaults()
    {
        var definitions = DefinitionSourceLoader
            .CreateInstances<IRoundEndDefinitionSource>()
            .SelectMany(source => source.GetTeamDefaults())
            .ToList();

        var duplicate = definitions
            .GroupBy(definition => definition.WinnerTeam)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate != null)
            throw new InvalidOperationException($"Duplicate round end team definition: {duplicate.Key}");

        return definitions.ToDictionary(definition => definition.WinnerTeam);
    }
}

/// <summary>
/// 定義に従ってラウンド終了処理を実行します。
/// </summary>
public static class RoundEndExecutor
{
    /// <summary>
    /// チームと特殊理由から定義を解決し、ラウンドを終了します。
    /// </summary>
    /// <param name="winnerTeam">勝利チーム。</param>
    /// <param name="specificReason">特殊理由。</param>
    /// <param name="tryUseTeamDefine">true の場合、Profile の Group 解決より先にチーム直接定義を試します。</param>
    public static void EndRound(CTeam winnerTeam, string? specificReason = null, bool tryUseTeamDefine = false)
    {
        if (tryUseTeamDefine && RoundEndDefinitions.TryGet(winnerTeam, specificReason, out var teamDefinition))
        {
            Execute(teamDefinition);
            return;
        }

        var resolvedWinnerTeam = CTeamProfileRegistry.ResolveWinnerTeam(winnerTeam);
        Execute(RoundEndDefinitions.Get(resolvedWinnerTeam, specificReason));
    }

    /// <summary>
    /// Group と特殊理由から定義を解決し、ラウンドを終了します。
    /// </summary>
    /// <param name="winnerGroup">勝利 Group。</param>
    /// <param name="specificReason">特殊理由。null の場合は Group 側の特殊理由を使います。</param>
    public static void EndRound(CTeamGroup winnerGroup, string? specificReason = null)
    {
        Execute(RoundEndDefinitions.Get(winnerGroup, specificReason));
    }

    /// <summary>
    /// 指定された定義に従ってラウンドを終了します。
    /// </summary>
    /// <param name="definition">ラウンド終了定義。</param>
    public static void Execute(RoundEndDefinition definition)
    {
        definition.BeforeEnd?.Invoke();
        ClearPlayerHints();
        ApplyScore(definition);

        if (definition.UseVanillaEndRound)
        {
            Round.EndRound(true);
            return;
        }

        if (!string.IsNullOrEmpty(definition.VictoryHint))
            ShowVictoryHint(definition);

        if (definition.RestartDelay is { } delay)
            ScheduleRestartIfRoundActive(delay);
    }

    private static void ApplyScore(RoundEndDefinition definition)
    {
        switch (definition.ScoreMode)
        {
            case RoundEndScoreMode.None:
                break;
            case RoundEndScoreMode.KillsByScp:
                Round.KillsByScp = definition.ScoreValue;
                break;
            case RoundEndScoreMode.EscapedDClasses:
                Round.EscapedDClasses = definition.ScoreValue;
                break;
            case RoundEndScoreMode.EscapedScientists:
                Round.EscapedScientists = definition.ScoreValue;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(definition.ScoreMode), definition.ScoreMode, null);
        }
    }

    private static void ClearPlayerHints()
    {
        foreach (var player in Player.List)
        {
            player?.ShowRueiPlus("");
        }
    }

    private static void ShowVictoryHint(RoundEndDefinition definition)
    {
        foreach (var player in Player.List)
        {
            player.ShowRueiPlus(definition.VictoryHint, definition.HintDuration);

            if (definition.OverrideIntercom)
                Intercom.TrySetOverride(player, true);
        }
    }

    private static void ScheduleRestartIfRoundActive(float delay)
    {
        Timing.RunCoroutine(DelayUnlessLobby(delay, StaticUtils.TryRestart));
    }

    private static IEnumerator<float> DelayUnlessLobby(float delay, Action action)
    {
        var remaining = delay;
        while (remaining > 0f)
        {
            if (Round.IsLobby)
                yield break;

            var wait = Math.Min(0.5f, remaining);
            remaining -= wait;
            yield return Timing.WaitForSeconds(wait);
        }

        if (Round.IsLobby)
            yield break;

        action();
    }
}
