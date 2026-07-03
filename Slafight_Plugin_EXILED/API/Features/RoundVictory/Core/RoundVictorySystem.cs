using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.Common;
using Slafight_Plugin_EXILED.API.Features.Teams.Profiles;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.CustomRoles;
using Slafight_Plugin_EXILED.CustomRoles.SCPs;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.MainHandlers;
using Slafight_Plugin_EXILED.SpecialEvents;

namespace Slafight_Plugin_EXILED.API.Features.RoundVictory.Core;

/// <summary>
/// 1 回の勝利判定で参照するラウンド状態をまとめます。
/// </summary>
public sealed class RoundVictoryContext
{
    /// <summary>
    /// 勝利判定コンテキストを作成します。
    /// </summary>
    /// <param name="alivePlayers">判定対象になる生存中の非スペクテイター。</param>
    public RoundVictoryContext(IReadOnlyList<Player> alivePlayers)
    {
        AlivePlayers = alivePlayers;
    }

    /// <summary>
    /// 判定対象になる生存中の非スペクテイター。
    /// </summary>
    public IReadOnlyList<Player> AlivePlayers { get; }

    /// <summary>
    /// 現在進行中の特殊イベント。
    /// </summary>
    public SpecialEventType ActiveEvent => SpecialEventsHandler.Instance?.NowEvent ?? SpecialEventType.None;

    /// <summary>
    /// 現在の季節設定。
    /// </summary>
    public SeasonTypeId Season => MapFlags.GetSeason();

    /// <summary>
    /// 現在有効なチーム/勝利判定プロファイル。
    /// </summary>
    public string TeamProfile => CTeamProfileManager.ActiveProfile;
}

/// <summary>
/// 特殊処理を伴う勝利・敗北条件を表します。
/// </summary>
/// <remarks>
/// 通常の「生存勢力が 1 グループだけ残った」判定では表現できない処理に使います。
/// 例: SCP-079 だけが SCP 側に残った場合の C.A.S.S.I.E. 終了、FacilityTermination の人類/正常性勝利。
/// </remarks>
public sealed class RoundVictoryCondition
{
    /// <summary>
    /// 特殊勝利条件を作成します。
    /// </summary>
    /// <param name="team">条件成立時に扱う勝利チーム。</param>
    /// <param name="debugName">ログに出す識別名。</param>
    /// <param name="check">条件が成立しているかを判定する関数。</param>
    /// <param name="execute">条件成立時に実行する処理。</param>
    /// <param name="isEnabled">この条件を有効にするかを判定する関数。null の場合は常に有効。</param>
    /// <param name="isForEnd">true の場合、実行後に勝利判定ループを終了します。</param>
    public RoundVictoryCondition(
        CTeam team,
        string debugName,
        Func<RoundVictoryContext, bool> check,
        Action execute,
        Func<RoundVictoryContext, bool>? isEnabled = null,
        bool isForEnd = true)
    {
        Team = team;
        DebugName = debugName;
        Check = check;
        Execute = execute;
        IsEnabled = isEnabled;
        IsForEnd = isForEnd;
    }

    /// <summary>
    /// 条件成立時に扱う勝利チーム。
    /// </summary>
    public CTeam Team { get; }

    /// <summary>
    /// ログに出す識別名。
    /// </summary>
    public string DebugName { get; }

    /// <summary>
    /// 条件が成立しているかを判定する関数。
    /// </summary>
    public Func<RoundVictoryContext, bool> Check { get; }

    /// <summary>
    /// 条件成立時に実行する処理。
    /// </summary>
    public Action Execute { get; }

    /// <summary>
    /// この条件を有効にするかを判定する関数。null の場合は常に有効です。
    /// </summary>
    public Func<RoundVictoryContext, bool>? IsEnabled { get; }

    /// <summary>
    /// true の場合、実行後に勝利判定ループを終了します。
    /// </summary>
    public bool IsForEnd { get; }

    /// <summary>
    /// 現在のコンテキストでこの条件が有効かを返します。
    /// </summary>
    /// <param name="context">勝利判定コンテキスト。</param>
    /// <returns>条件が有効な場合は true。</returns>
    public bool CanRun(RoundVictoryContext context) => IsEnabled?.Invoke(context) ?? true;
}

/// <summary>
/// 通常ラウンドで勝利勢力として数えるプレイヤー集合を表します。
/// </summary>
/// <remarks>
/// 評価器は、有効な勝利グループのうち実際に生存者を含むものが 1 つだけになった場合に勝利と判定します。
/// たとえば GoC が手動スポーンされた場合でも、<see cref="IncludesPlayer"/> に一致する生存者がいる限り
/// 自動で勝利判定に含まれます。
/// </remarks>
public sealed class RoundVictoryGroup
{
    /// <summary>
    /// 勝利グループを作成します。
    /// </summary>
    /// <param name="debugName">ログに出す識別名。</param>
    /// <param name="winnerTeam">このグループだけが残ったときに勝利させるチーム。</param>
    /// <param name="includesPlayer">プレイヤーがこの勝利グループに属するかを判定する関数。</param>
    /// <param name="specificReason"><see cref="CustomRolesHandler.EndRound"/> に渡す特殊理由。</param>
    /// <param name="isEnabled">このグループを有効にするかを判定する関数。null の場合は常に有効。</param>
    /// <param name="requiresVanillaEndLock">このグループの生存者がいる間、vanilla の EndingRound を止めるか。</param>
    /// <param name="priority">評価順。小さい値ほど先に評価され、同じプレイヤーが複数グループに該当し得る場合の所属解決に使います。</param>
    public RoundVictoryGroup(
        string debugName,
        CTeam winnerTeam,
        Func<Player, bool> includesPlayer,
        string? specificReason = null,
        Func<RoundVictoryContext, bool>? isEnabled = null,
        bool requiresVanillaEndLock = false,
        int priority = 100)
    {
        DebugName = debugName;
        WinnerTeam = winnerTeam;
        IncludesPlayer = includesPlayer;
        SpecificReason = specificReason;
        IsEnabled = isEnabled;
        RequiresVanillaEndLock = requiresVanillaEndLock;
        Priority = priority;
    }

    /// <summary>
    /// ログに出す識別名。
    /// </summary>
    public string DebugName { get; }

    /// <summary>
    /// このグループだけが残ったときに勝利させるチーム。
    /// </summary>
    public CTeam WinnerTeam { get; }

    /// <summary>
    /// <see cref="CustomRolesHandler.EndRound"/> に渡す特殊理由。
    /// </summary>
    public string? SpecificReason { get; }

    /// <summary>
    /// プレイヤーがこの勝利グループに属するかを判定する関数。
    /// </summary>
    public Func<Player, bool> IncludesPlayer { get; }

    /// <summary>
    /// このグループを有効にするかを判定する関数。null の場合は常に有効です。
    /// </summary>
    public Func<RoundVictoryContext, bool>? IsEnabled { get; }

    /// <summary>
    /// このグループの生存者がいる間、vanilla の EndingRound を止めるか。
    /// </summary>
    /// <remarks>
    /// EXILED/vanilla 側が知らない独自勢力が残っている場合、標準の終了判定が先に走ることがあります。
    /// そのような独自勝利勢力は true にしてください。
    /// </remarks>
    public bool RequiresVanillaEndLock { get; }

    /// <summary>
    /// 評価順。小さい値ほど先に評価されます。
    /// </summary>
    /// <remarks>
    /// 現在はグループ一覧の安定した評価順として使います。
    /// 同じプレイヤーが複数グループに該当し得る定義を追加する場合は、
    /// より特殊なグループほど小さい値にしてください。
    /// </remarks>
    public int Priority { get; }

    /// <summary>
    /// 現在のコンテキストでこのグループが有効かを返します。
    /// </summary>
    /// <param name="context">勝利判定コンテキスト。</param>
    /// <returns>グループが有効な場合は true。</returns>
    public bool CanRun(RoundVictoryContext context) => IsEnabled?.Invoke(context) ?? true;

    public static RoundVictoryGroup ForTeams(
        string debugName,
        CTeam winnerTeam,
        IReadOnlyCollection<CTeam> memberTeams,
        string? specificReason = null,
        Func<RoundVictoryContext, bool>? isEnabled = null,
        bool requiresVanillaEndLock = false,
        int priority = 100)
    {
        var teams = new HashSet<CTeam>(memberTeams);
        return new RoundVictoryGroup(
            debugName,
            winnerTeam,
            player => teams.Contains(RoundVictoryDefinitions.GetVictoryTeam(player)),
            specificReason,
            isEnabled,
            requiresVanillaEndLock,
            priority);
    }

    public static RoundVictoryGroup ForCustomRoles(
        string debugName,
        CTeam winnerTeam,
        IReadOnlyCollection<CRoleTypeId> customRoles,
        string? specificReason = null,
        Func<RoundVictoryContext, bool>? isEnabled = null,
        bool requiresVanillaEndLock = false,
        int priority = 100)
    {
        var roles = new HashSet<CRoleTypeId>(customRoles);
        return new RoundVictoryGroup(
            debugName,
            winnerTeam,
            player => roles.Contains(player.GetCustomRole()),
            specificReason,
            isEnabled,
            requiresVanillaEndLock,
            priority);
    }
}

/// <summary>
/// 勝利判定の評価結果を表します。
/// </summary>
public sealed class RoundVictoryResult
{
    private RoundVictoryResult(
        bool triggered,
        string debugName,
        CTeam team,
        bool isForEnd,
        Action? execute)
    {
        Triggered = triggered;
        DebugName = debugName;
        Team = team;
        IsForEnd = isForEnd;
        Execute = execute;
    }

    public static RoundVictoryResult None { get; } =
        new(false, string.Empty, CTeam.Null, false, null);

    /// <summary>
    /// 勝利条件が成立したか。
    /// </summary>
    public bool Triggered { get; }

    /// <summary>
    /// 成立した条件またはグループの識別名。
    /// </summary>
    public string DebugName { get; }

    /// <summary>
    /// 勝利チーム。
    /// </summary>
    public CTeam Team { get; }

    /// <summary>
    /// true の場合、実行後に勝利判定ループを終了します。
    /// </summary>
    public bool IsForEnd { get; }

    /// <summary>
    /// 勝利成立時に実行する処理。
    /// </summary>
    public Action? Execute { get; }

    /// <summary>
    /// 特殊勝利条件から評価結果を作成します。
    /// </summary>
    /// <param name="condition">成立した特殊勝利条件。</param>
    /// <returns>勝利判定結果。</returns>
    public static RoundVictoryResult FromCondition(RoundVictoryCondition condition) =>
        new(true, condition.DebugName, condition.Team, condition.IsForEnd, condition.Execute);

    /// <summary>
    /// 通常勝利グループから評価結果を作成します。
    /// </summary>
    /// <param name="group">勝利したグループ。</param>
    /// <returns>勝利判定結果。</returns>
    public static RoundVictoryResult FromGroup(RoundVictoryGroup group) =>
        new(
            true,
            group.DebugName,
            group.WinnerTeam,
            true,
            () => CustomRolesHandler.EndRound(group.WinnerTeam, group.SpecificReason));

    public static RoundVictoryResult ForTeam(string debugName, CTeam team, string? specificReason = null) =>
        new(
            true,
            debugName,
            team,
            true,
            () => CustomRolesHandler.EndRound(team, specificReason));
}

public interface IRoundVictoryDefinitionSource
{
    bool IsEnabled(RoundVictoryContext context);
    IEnumerable<RoundVictoryCondition> GetConditions();
    IEnumerable<RoundVictoryGroup> GetGroups();
}

public abstract class RoundVictoryDefinitionSource : IRoundVictoryDefinitionSource
{
    public virtual bool IsEnabled(RoundVictoryContext context) => true;

    public virtual IEnumerable<RoundVictoryCondition> GetConditions()
    {
        yield break;
    }

    public virtual IEnumerable<RoundVictoryGroup> GetGroups()
    {
        yield break;
    }
}

/// <summary>
/// ラウンド勝利判定の定義一覧を提供します。
/// </summary>
public static class RoundVictoryDefinitions
{
    private static readonly Lazy<IReadOnlyList<IRoundVictoryDefinitionSource>> Sources =
        new(DefinitionSourceLoader.CreateInstances<IRoundVictoryDefinitionSource>);

    /// <summary>
    /// 現在のコンテキストに対応する特殊勝利条件一覧を取得します。
    /// </summary>
    /// <param name="context">勝利判定コンテキスト。</param>
    /// <returns>評価対象の特殊勝利条件一覧。</returns>
    public static IReadOnlyList<RoundVictoryCondition> GetConditions(RoundVictoryContext context)
    {
        return Sources.Value
            .Where(source => source.IsEnabled(context))
            .SelectMany(source => source.GetConditions())
            .ToList();
    }

    /// <summary>
    /// 現在のコンテキストに対応する通常勝利グループ一覧を取得します。
    /// </summary>
    /// <param name="context">勝利判定コンテキスト。</param>
    /// <returns>評価対象の通常勝利グループ一覧。</returns>
    public static IReadOnlyList<RoundVictoryGroup> GetGroups(RoundVictoryContext context)
    {
        return Sources.Value
            .Where(source => source.IsEnabled(context))
            .SelectMany(source => source.GetGroups())
            .ToList();
    }

    /// <summary>
    /// 指定プレイヤーが vanilla 終了判定を止めるべき勝利グループに属するかを返します。
    /// </summary>
    /// <param name="player">確認対象のプレイヤー。</param>
    /// <returns>標準終了を止めるべき場合は true。</returns>
    public static bool RequiresVanillaEndLock(Player player)
    {
        if (!IsAliveRoundPlayer(player))
            return false;

        var context = new RoundVictoryContext([player]);
        return GetGroups(context)
            .Any(group => group.RequiresVanillaEndLock &&
                          group.CanRun(context) &&
                          group.IncludesPlayer(player));
    }

    internal static CTeam GetVictoryTeam(Player? player)
    {
        if (player == null)
            return CTeam.Null;

        if (Scp076Role.IsFoundationAlignedForVictory(player))
            return CTeam.FoundationForces;

        return player.GetTeam();
    }

    internal static bool HasOnlyTargetRoleOnSideAgainstSingleOpposingTeam(
        IReadOnlyList<Player> players,
        Func<Player, bool> isSideMember,
        Func<Player, bool> isTargetRole)
    {
        int sidePlayerCount = 0;
        int targetRoleCount = 0;
        HashSet<CTeam> opposingTeams = [];

        foreach (var player in players)
        {
            if (!IsAliveRoundPlayer(player))
                continue;

            if (isSideMember(player))
            {
                sidePlayerCount++;

                if (isTargetRole(player))
                {
                    targetRoleCount++;
                    continue;
                }

                return false;
            }

            opposingTeams.Add(GetVictoryTeam(player));
        }

        return sidePlayerCount > 0 &&
               targetRoleCount == sidePlayerCount &&
               opposingTeams.Count == 1;
    }

    internal static bool IsScp079Player(Player player)
    {
        return IsAliveRoundPlayer(player) &&
               player.GetTeam() == CTeam.SCPs &&
               player.Role.Type == RoleTypeId.Scp079;
    }

    internal static bool IsAraOrunPlayer(Player player)
    {
        return IsAliveRoundPlayer(player) &&
               player.GetCustomRole() == CRoleTypeId.AraOrun;
    }

    internal static bool IsAliveRoundPlayer(Player? player)
    {
        return player != null &&
               player.IsAlive &&
               player.Role.Type != RoleTypeId.Spectator &&
               !CRole.IsTeamNpc(player);
    }

    internal static void ExecuteAIKill()
    {
        var terminated = TerminatePlayers(IsScp079Player, "Terminated by C.A.S.S.I.E.");

        if (terminated == 0)
            return;

        Exiled.API.Features.Cassie.MessageTranslated(
            "SCP-079 has been terminated by Central Autonomic Service System for Internal Emergencies.",
            "<color=red>SCP-079</color>は<color=yellow>C.A.S.S.I.E</color>により終了されました。");
        NewEventHandler.RecoverControl(FacilityControlRecoverType.DisableTesla);
    }

    internal static void ExecuteAraOrunKill()
    {
        var terminated = TerminatePlayers(IsAraOrunPlayer, "Terminated by Fifthists");

        if (terminated == 0)
            return;

        Exiled.API.Features.Cassie.MessageTranslated(
            "Unknown Subject has been terminated by SCP 3 1 2 5",
            $"<color=yellow>アラ・オルン</color>は<color={CTeam.Fifthists.GetTeamColor()}>SCP-3125</color>により終了されました。");
    }

    private static int TerminatePlayers(Func<Player, bool> selector, string reason)
    {
        var targets = Player.List
            .Where(player => player != null && selector(player))
            .ToList();

        foreach (var target in targets)
        {
            target.Kill(reason);
        }

        return targets.Count;
    }
}

/// <summary>
/// 現在の生存者からラウンド勝利を評価します。
/// </summary>
public static class RoundVictoryEvaluator
{
    /// <summary>
    /// 勝利条件を評価します。
    /// </summary>
    /// <param name="alivePlayers">判定対象になる生存中の非スペクテイター。</param>
    /// <returns>成立した勝利結果。未成立の場合は <see cref="RoundVictoryResult.None"/>。</returns>
    public static RoundVictoryResult Evaluate(IReadOnlyList<Player> alivePlayers)
    {
        var context = new RoundVictoryContext(alivePlayers);

        foreach (var condition in RoundVictoryDefinitions.GetConditions(context))
        {
            if (!condition.CanRun(context))
                continue;

            if (condition.Check(context))
                return RoundVictoryResult.FromCondition(condition);
        }

        return EvaluateGroupDominance(context);
    }

    private static RoundVictoryResult EvaluateGroupDominance(RoundVictoryContext context)
    {
        if (EvacuationRoundEndState.ShouldDeferRoundEnd(context.AlivePlayers))
            return RoundVictoryResult.None;

        if (context.AlivePlayers.Count == 0 &&
            EvacuationRoundEndState.TryCreateAllEscapedResult(out var allEscapedResult))
            return allEscapedResult;

        var groups = RoundVictoryDefinitions.GetGroups(context)
            .Where(group => group.CanRun(context))
            .OrderBy(group => group.Priority)
            .ToList();

        if (groups.Count == 0)
            return RoundVictoryResult.None;

        var activeGroups = context.AlivePlayers
            .Select(player => groups.FirstOrDefault(group => group.IncludesPlayer(player)))
            .Where(group => group != null)
            .Distinct()
            .ToList();

        if (activeGroups.Count != 1)
            return RoundVictoryResult.None;

        return RoundVictoryResult.FromGroup(activeGroups[0]);
    }
}
