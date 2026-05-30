using System;
using System.Collections.Generic;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.API.Features.RoundVictory.Core;

/// <summary>
/// チーム定義側から選べる通常勝利条件プリセットです。
/// </summary>
public sealed class RoundVictoryRule
{
    private RoundVictoryRule(
        string debugName,
        CTeam winnerTeam,
        Func<Player, bool> includesPlayer,
        string? specificReason,
        Func<RoundVictoryContext, bool>? isEnabled,
        bool requiresVanillaEndLock,
        int priority)
    {
        DebugName = debugName;
        WinnerTeam = winnerTeam;
        IncludesPlayer = includesPlayer;
        SpecificReason = specificReason;
        IsEnabled = isEnabled;
        RequiresVanillaEndLock = requiresVanillaEndLock;
        Priority = priority;
    }

    public string DebugName { get; }
    public CTeam WinnerTeam { get; }
    public Func<Player, bool> IncludesPlayer { get; }
    public string? SpecificReason { get; }
    public Func<RoundVictoryContext, bool>? IsEnabled { get; }
    public bool RequiresVanillaEndLock { get; }
    public int Priority { get; }

    public RoundVictoryGroup ToGroup() =>
        new(DebugName, WinnerTeam, IncludesPlayer, SpecificReason, IsEnabled, RequiresVanillaEndLock, Priority);

    /// <summary>
    /// 指定したチームだけを勝利側として数えます。
    /// </summary>
    public static RoundVictoryRule ForTeam(
        CTeam winnerTeam,
        int priority,
        string? debugName = null,
        string? specificReason = null,
        bool requiresVanillaEndLock = false,
        Func<RoundVictoryContext, bool>? isEnabled = null)
    {
        return ForTeamSide(
            winnerTeam,
            priority,
            memberTeams: [winnerTeam],
            debugName,
            specificReason,
            requiresVanillaEndLock,
            isEnabled);
    }

    /// <summary>
    /// 複数チームを同じ勝利側として数え、ラウンド終了時は winnerTeam の勝利として扱います。
    /// </summary>
    public static RoundVictoryRule ForTeamSide(
        CTeam winnerTeam,
        int priority,
        IReadOnlyCollection<CTeam> memberTeams,
        string? debugName = null,
        string? specificReason = null,
        bool requiresVanillaEndLock = false,
        Func<RoundVictoryContext, bool>? isEnabled = null)
    {
        var teams = new HashSet<CTeam>(memberTeams);
        return ForPredicate(
            debugName ?? $"{winnerTeam}Win",
            winnerTeam,
            player => teams.Contains(player.GetTeam()),
            priority,
            specificReason,
            requiresVanillaEndLock,
            isEnabled);
    }

    /// <summary>
    /// winnerTeam と alliedTeams を同じ勝利側として数え、仲間チームが残っていても winnerTeam 勝利で終わらせます。
    /// </summary>
    public static RoundVictoryRule ForTeamWithAllies(
        CTeam winnerTeam,
        int priority,
        IReadOnlyCollection<CTeam> alliedTeams,
        string? debugName = null,
        string? specificReason = null,
        bool requiresVanillaEndLock = false,
        Func<RoundVictoryContext, bool>? isEnabled = null)
    {
        var memberTeams = new HashSet<CTeam>(alliedTeams) { winnerTeam };
        return ForTeamSide(
            winnerTeam,
            priority,
            memberTeams,
            debugName,
            specificReason,
            requiresVanillaEndLock,
            isEnabled);
    }

    /// <summary>
    /// 指定したカスタムロールだけを勝利側として数えます。
    /// </summary>
    public static RoundVictoryRule ForCustomRoles(
        CTeam winnerTeam,
        int priority,
        IReadOnlyCollection<CRoleTypeId> customRoles,
        string? debugName = null,
        string? specificReason = null,
        bool requiresVanillaEndLock = false,
        Func<RoundVictoryContext, bool>? isEnabled = null)
    {
        var roles = new HashSet<CRoleTypeId>(customRoles);
        return ForPredicate(
            debugName ?? $"{winnerTeam}CustomRoleWin",
            winnerTeam,
            player => roles.Contains(player.GetCustomRole()),
            priority,
            specificReason,
            requiresVanillaEndLock,
            isEnabled);
    }

    /// <summary>
    /// 独自のプレイヤー判定を勝利側として数えます。
    /// </summary>
    public static RoundVictoryRule ForPredicate(
        string debugName,
        CTeam winnerTeam,
        Func<Player, bool> includesPlayer,
        int priority,
        string? specificReason = null,
        bool requiresVanillaEndLock = false,
        Func<RoundVictoryContext, bool>? isEnabled = null)
    {
        return new RoundVictoryRule(
            debugName,
            winnerTeam,
            includesPlayer,
            specificReason,
            isEnabled,
            requiresVanillaEndLock,
            priority);
    }
}
