using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using GameCore;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.Teams.Profiles;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.API.Features.RoundVictory.Core;

/// <summary>
/// RoundVictory system によってラウンド終了が確定した時の情報を表します。
/// </summary>
public sealed class RoundVictoryEndedEventArgs : EventArgs
{
    internal RoundVictoryEndedEventArgs(
        RoundEndDefinition definition,
        CTeam winnerTeam,
        string? specificReason,
        LeadingTeam leadingTeam,
        RoundSummary.SumInfo_ClassList classList,
        RoundSummary.SumInfo_ClassList initialClassList,
        IReadOnlyList<Player> alivePlayers,
        string teamProfile,
        DateTime endedAtUtc,
        bool useVanillaEndRound,
        float? restartDelay,
        int? timeToRestart)
    {
        Definition = definition;
        WinnerTeam = winnerTeam;
        SpecificReason = specificReason;
        LeadingTeam = leadingTeam;
        ClassList = classList;
        InitialClassList = initialClassList;
        AlivePlayers = alivePlayers;
        TeamProfile = teamProfile;
        EndedAtUtc = endedAtUtc;
        UseVanillaEndRound = useVanillaEndRound;
        RestartDelay = restartDelay;
        TimeToRestart = timeToRestart;
    }

    /// <summary>
    /// ラウンド終了定義。
    /// </summary>
    public RoundEndDefinition Definition { get; }

    /// <summary>
    /// ログ・調査用の終了定義名。
    /// </summary>
    public string DebugName => Definition.DebugName;

    /// <summary>
    /// 勝利チーム。
    /// </summary>
    public CTeam WinnerTeam { get; }

    /// <summary>
    /// 特殊終了理由。
    /// </summary>
    public string? SpecificReason { get; }

    /// <summary>
    /// EXILED の RoundEndedEventArgs に近い勝利勢力。
    /// カスタム勢力など vanilla に対応しないチームは Draw になります。
    /// </summary>
    public LeadingTeam LeadingTeam { get; }

    /// <summary>
    /// 終了確定時点の vanilla 形式クラス集計。
    /// </summary>
    public RoundSummary.SumInfo_ClassList ClassList { get; }

    /// <summary>
    /// ラウンド開始時の vanilla 形式クラス集計。
    /// </summary>
    public RoundSummary.SumInfo_ClassList InitialClassList { get; }

    /// <summary>
    /// 終了確定時点で生存していたプレイヤーのスナップショット。
    /// </summary>
    public IReadOnlyList<Player> AlivePlayers { get; }

    /// <summary>
    /// 終了確定時点で有効だったチームプロファイル。
    /// </summary>
    public string TeamProfile { get; }

    /// <summary>
    /// 終了確定時刻。
    /// </summary>
    public DateTime EndedAtUtc { get; }

    /// <summary>
    /// vanilla の Round.EndRound を使って終了する定義か。
    /// </summary>
    public bool UseVanillaEndRound { get; }

    /// <summary>
    /// 独自終了後の再起動予定秒数。vanilla 終了の場合は null です。
    /// </summary>
    public float? RestartDelay { get; }

    /// <summary>
    /// 再起動までの秒数。vanilla 終了では server config の auto_round_restart_time を反映します。
    /// </summary>
    public int? TimeToRestart { get; }

    /// <summary>
    /// 全体に表示される独自勝利ヒント。
    /// </summary>
    public string? VictoryHint => Definition.VictoryHint;

    /// <summary>
    /// 勝利表示中にインターコム override を有効化するか。
    /// </summary>
    public bool OverrideIntercom => Definition.OverrideIntercom;

    internal static RoundVictoryEndedEventArgs Create(
        RoundEndDefinition definition,
        LeadingTeam? leadingTeam = null,
        RoundSummary.SumInfo_ClassList? classList = null,
        int? timeToRestart = null)
    {
        return new RoundVictoryEndedEventArgs(
            definition,
            definition.WinnerTeam,
            definition.SpecificReason,
            leadingTeam ?? ToLeadingTeam(definition.WinnerTeam),
            classList ?? CreateClassListSnapshot(),
            GetInitialClassListSnapshot(),
            GetAlivePlayersSnapshot(),
            CTeamProfileManager.ActiveProfile,
            DateTime.UtcNow,
            definition.UseVanillaEndRound,
            definition.UseVanillaEndRound ? null : definition.RestartDelay,
            timeToRestart ?? GetTimeToRestart(definition));
    }

    private static int? GetTimeToRestart(RoundEndDefinition definition)
    {
        if (definition.UseVanillaEndRound)
            return Math.Max(5, Math.Min(1000, ConfigFile.ServerConfig.GetInt("auto_round_restart_time", 10)));

        return definition.RestartDelay.HasValue
            ? (int)Math.Ceiling(definition.RestartDelay.Value)
            : null;
    }

    private static IReadOnlyList<Player> GetAlivePlayersSnapshot()
    {
        return Player.List
            .Where(player => player != null &&
                             player.IsAlive &&
                             player.Role.Type != RoleTypeId.Spectator &&
                             player.IsSafePlayer() &&
                             !CRole.IsTeamNpc(player))
            .ToList();
    }

    private static RoundSummary.SumInfo_ClassList GetInitialClassListSnapshot()
    {
        return RoundSummary._singletonSet && RoundSummary.singleton != null
            ? RoundSummary.singleton.classlistStart
            : default;
    }

    private static RoundSummary.SumInfo_ClassList CreateClassListSnapshot()
    {
        var classList = new RoundSummary.SumInfo_ClassList();

        foreach (var player in Player.List)
        {
            switch (player.Role.Team)
            {
                case Team.ClassD:
                    classList.class_ds++;
                    break;
                case Team.ChaosInsurgency:
                    classList.chaos_insurgents++;
                    break;
                case Team.FoundationForces:
                    classList.mtf_and_guards++;
                    break;
                case Team.Scientists:
                    classList.scientists++;
                    break;
                case Team.Flamingos:
                    classList.flamingos++;
                    break;
                case Team.SCPs:
                    if (player.Role.Type == RoleTypeId.Scp0492)
                        classList.zombies++;
                    else
                        classList.scps_except_zombies++;
                    break;
            }
        }

        classList.warhead_kills = AlphaWarheadController.Detonated
            ? AlphaWarheadController.Singleton.WarheadKills
            : -1;

        return classList;
    }

    private static LeadingTeam ToLeadingTeam(CTeam team)
    {
        return team switch
        {
            CTeam.FoundationForces or CTeam.Scientists or CTeam.Guards => LeadingTeam.FacilityForces,
            CTeam.ClassD or CTeam.ChaosInsurgency => LeadingTeam.ChaosInsurgency,
            CTeam.SCPs => LeadingTeam.Anomalies,
            _ => LeadingTeam.Draw,
        };
    }
}

/// <summary>
/// RoundVictory system のイベントを提供します。
/// </summary>
public static class RoundVictoryEvents
{
    private static bool _registered;
    private static RoundEndDefinition? _pendingVanillaRoundEnd;

    /// <summary>
    /// RoundVictory system がラウンド終了を確定した時に発火します。
    /// </summary>
    public static event Action<RoundVictoryEndedEventArgs>? RoundEnded;

    internal static void Register()
    {
        if (_registered)
            return;

        Exiled.Events.Handlers.Server.RoundEnded += OnExiledRoundEnded;
        Exiled.Events.Handlers.Server.RestartingRound += ClearPendingVanillaRoundEnd;
        Exiled.Events.Handlers.Server.WaitingForPlayers += ClearPendingVanillaRoundEnd;
        _registered = true;
    }

    internal static void Unregister()
    {
        if (!_registered)
        {
            ClearPendingVanillaRoundEnd();
            return;
        }

        Exiled.Events.Handlers.Server.RoundEnded -= OnExiledRoundEnded;
        Exiled.Events.Handlers.Server.RestartingRound -= ClearPendingVanillaRoundEnd;
        Exiled.Events.Handlers.Server.WaitingForPlayers -= ClearPendingVanillaRoundEnd;
        ClearPendingVanillaRoundEnd();
        _registered = false;
    }

    internal static void MarkPendingVanillaRoundEnd(RoundEndDefinition definition)
    {
        _pendingVanillaRoundEnd = definition;
    }

    internal static void OnRoundEnded(RoundVictoryEndedEventArgs ev)
    {
        var handlers = RoundEnded;
        if (handlers == null)
            return;

        foreach (Action<RoundVictoryEndedEventArgs> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(ev);
            }
            catch (Exception ex)
            {
                Log.Error($"[RoundVictoryEvents] RoundEnded handler failed: {ex}");
            }
        }
    }

    private static void OnExiledRoundEnded(Exiled.Events.EventArgs.Server.RoundEndedEventArgs ev)
    {
        var definition = _pendingVanillaRoundEnd;
        if (definition == null)
            return;

        _pendingVanillaRoundEnd = null;
        OnRoundEnded(RoundVictoryEndedEventArgs.Create(definition, ev.LeadingTeam, ev.ClassList, ev.TimeToRestart));
    }

    private static void ClearPendingVanillaRoundEnd()
    {
        _pendingVanillaRoundEnd = null;
    }
}
