using System;
using System.Collections.Generic;
using Exiled.API.Features;
using MEC;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.API.Features.RoundVictory;

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
        float? restartDelay = 10f)
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
public static class RoundEndDefinitions
{
    private static readonly IReadOnlyDictionary<RoundEndDefinitionKey, RoundEndDefinition> SpecificDefinitions =
        new Dictionary<RoundEndDefinitionKey, RoundEndDefinition>
        {
            [Key(CTeam.FoundationForces, RoundEndReasons.NoHumanityAllowed)] = new(
                "FacilityTerminationNormalcyWin",
                CTeam.FoundationForces,
                RoundEndReasons.NoHumanityAllowed,
                RoundEndScoreMode.EscapedScientists,
                victoryHint: "<b><size=80><color=red>正常性</color>の勝利</size></b>"),

            [Key(CTeam.Others, RoundEndReasons.SnowWarrierWin)] = new(
                "SnowWarrierWin",
                CTeam.Others,
                RoundEndReasons.SnowWarrierWin,
                RoundEndScoreMode.EscapedDClasses,
                victoryHint: "<b><size=80><color=#ffffff>雪の戦士達</color>の勝利</size></b>"),

            [Key(CTeam.Others, RoundEndReasons.CandyWarrierWin)] = new(
                "CandyWarrierWin",
                CTeam.Others,
                RoundEndReasons.CandyWarrierWin,
                RoundEndScoreMode.EscapedDClasses,
                victoryHint: "<b><size=80><color=#ff96de>お菓子の戦士達</color>の勝利</size></b>"),

            [Key(CTeam.GoC, RoundEndReasons.SavedHumanity)] = new(
                "FacilityTerminationHumanityWin",
                CTeam.GoC,
                RoundEndReasons.SavedHumanity,
                RoundEndScoreMode.EscapedDClasses,
                victoryHint: "<b><size=80><color=#0000c8>人類</color>の勝利</size></b>"),
        };

    private static readonly IReadOnlyDictionary<CTeam, RoundEndDefinition> TeamDefaults =
        new Dictionary<CTeam, RoundEndDefinition>
        {
            [CTeam.SCPs] = Vanilla(
                "ScpWin",
                CTeam.SCPs,
                RoundEndScoreMode.KillsByScp),

            [CTeam.Fifthists] = Custom(
                "FifthistWin",
                CTeam.Fifthists,
                "<b><size=80><color=#ff00fa>第五教会</color>の勝利</size></b>",
                RoundEndScoreMode.KillsByScp,
                555),

            [CTeam.ChaosInsurgency] = Vanilla(
                "ChaosWin",
                CTeam.ChaosInsurgency,
                RoundEndScoreMode.EscapedDClasses),

            [CTeam.ClassD] = Vanilla(
                "ClassDWin",
                CTeam.ClassD,
                RoundEndScoreMode.EscapedDClasses),

            [CTeam.FoundationForces] = Vanilla(
                "FoundationWin",
                CTeam.FoundationForces,
                RoundEndScoreMode.EscapedScientists),

            [CTeam.Scientists] = Vanilla(
                "ScientistsWin",
                CTeam.Scientists,
                RoundEndScoreMode.EscapedScientists),

            [CTeam.Guards] = Vanilla(
                "GuardsWin",
                CTeam.Guards,
                RoundEndScoreMode.EscapedScientists),

            [CTeam.Others] = Custom(
                "UnknownOthersWin",
                CTeam.Others,
                "<b><size=80><color=#ffffff>UNKNOWN TEAM</color>の勝利</size></b>",
                RoundEndScoreMode.EscapedDClasses),

            [CTeam.GoC] = Custom(
                "GoCWin",
                CTeam.GoC,
                "<b><size=80><color=#0000c8>世界オカルト連合</color>の勝利</size></b>",
                RoundEndScoreMode.EscapedDClasses),
        };

    private static readonly RoundEndDefinition UnknownDefinition = Custom(
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
        if (!string.IsNullOrEmpty(specificReason) &&
            SpecificDefinitions.TryGetValue(Key(winnerTeam, specificReason), out var specificDefinition))
        {
            return specificDefinition;
        }

        return TeamDefaults.TryGetValue(winnerTeam, out var teamDefinition)
            ? teamDefinition
            : UnknownDefinition;
    }

    private static RoundEndDefinition Vanilla(
        string debugName,
        CTeam winnerTeam,
        RoundEndScoreMode scoreMode) =>
        new(debugName, winnerTeam, scoreMode: scoreMode, useVanillaEndRound: true, restartDelay: null);

    private static RoundEndDefinition Custom(
        string debugName,
        CTeam winnerTeam,
        string victoryHint,
        RoundEndScoreMode scoreMode = RoundEndScoreMode.None,
        int scoreValue = 999) =>
        new(debugName, winnerTeam, scoreMode: scoreMode, scoreValue: scoreValue, victoryHint: victoryHint);

    private static RoundEndDefinitionKey Key(CTeam team, string? specificReason) =>
        new(team, specificReason);
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
    public static void EndRound(CTeam winnerTeam, string? specificReason = null)
    {
        Execute(RoundEndDefinitions.Get(winnerTeam, specificReason));
    }

    /// <summary>
    /// 指定された定義に従ってラウンドを終了します。
    /// </summary>
    /// <param name="definition">ラウンド終了定義。</param>
    public static void Execute(RoundEndDefinition definition)
    {
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
            player?.ShowHint("");
        }
    }

    private static void ShowVictoryHint(RoundEndDefinition definition)
    {
        foreach (var player in Player.List)
        {
            player.ShowHint(definition.VictoryHint, definition.HintDuration);

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
