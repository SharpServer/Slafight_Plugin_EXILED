using System;
using System.Collections.Generic;
using System.Linq;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Features.RoundVictory;

namespace Slafight_Plugin_EXILED.API.Features.Teams;

/// <summary>
/// 1 つの <see cref="CTeam"/> を Profile 内の <see cref="CTeamGroup"/> に割り当てる行。
/// </summary>
public readonly struct CTeamProfileTeamGroup
{
    /// <summary>
    /// チーム割当を作成します。
    /// </summary>
    /// <param name="team">割当対象のチーム。</param>
    /// <param name="group">Profile 内で所属させる Group。</param>
    public CTeamProfileTeamGroup(CTeam team, CTeamGroup group)
    {
        Team = team;
        Group = group;
    }

    /// <summary>
    /// 割当対象のチーム。
    /// </summary>
    public CTeam Team { get; }

    /// <summary>
    /// Profile 内で所属する Group。
    /// </summary>
    public CTeamGroup Group { get; }
}

/// <summary>
/// 1 つのチーム/勝利判定 Profile の定義。
/// </summary>
/// <remarks>
/// Profile は「各 <see cref="CTeam"/> がどの <see cref="CTeamGroup"/> に属するか」と、
/// 「その Group が勝った時にどの <see cref="CTeam"/> と終了理由で終わるか」をまとめて持ちます。
/// 新しい特殊イベントを追加する場合は、基本的にこの Profile 定義を 1 つ追加します。
/// </remarks>
public sealed class CTeamProfileDefinition
{
    /// <summary>
    /// Profile 定義を作成します。
    /// </summary>
    /// <param name="profileKey">Profile の識別子。</param>
    /// <param name="teamGroups">全 <see cref="CTeam"/> の Group 割当。</param>
    /// <param name="victories">Group 勝利時の終了方法。<see cref="CTeamGroup.Undefined"/> には定義しません。</param>
    public CTeamProfileDefinition(
        string profileKey,
        IEnumerable<CTeamProfileTeamGroup> teamGroups,
        IEnumerable<CTeamProfileVictoryDefinition> victories)
    {
        ProfileKey = profileKey;
        TeamGroups = teamGroups.ToDictionary(mapping => mapping.Team, mapping => mapping.Group);
        Victories = victories
            .Select(victory => victory.WithProfile(profileKey))
            .ToDictionary(victory => victory.Group);
    }

    /// <summary>
    /// Profile の識別子。
    /// </summary>
    public string ProfileKey { get; }

    /// <summary>
    /// この Profile での <see cref="CTeam"/> から <see cref="CTeamGroup"/> への割当表。
    /// </summary>
    /// <remarks>
    /// 全 <see cref="CTeam"/> を埋めます。Group 勝利に参加させないチームは
    /// <see cref="CTeamGroup.Undefined"/> を明示します。
    /// </remarks>
    public IReadOnlyDictionary<CTeam, CTeamGroup> TeamGroups { get; }

    /// <summary>
    /// Group 勝利時の終了方法。
    /// </summary>
    public IReadOnlyDictionary<CTeamGroup, CTeamProfileVictoryDefinition> Victories { get; }

    /// <summary>
    /// 指定チームの Profile 内 Group を取得します。
    /// </summary>
    public bool TryGetGroup(CTeam team, out CTeamGroup group) =>
        TeamGroups.TryGetValue(team, out group);

    /// <summary>
    /// 指定 Group の勝利定義を取得します。
    /// </summary>
    public bool TryGetVictory(CTeamGroup group, out CTeamProfileVictoryDefinition victory) =>
        Victories.TryGetValue(group, out victory);

    /// <summary>
    /// 指定 Group に所属するチーム一覧を取得します。
    /// </summary>
    public IReadOnlyCollection<CTeam> GetTeams(CTeamGroup group) =>
        TeamGroups
            .Where(pair => pair.Value == group)
            .Select(pair => pair.Key)
            .ToList();
}

/// <summary>
/// Profile 内の Group が勝利した時の終了方法。
/// </summary>
/// <remarks>
/// ここに書くのは Group 側の勝ち方です。チーム単体の特殊な勝ち方は
/// <see cref="CTeamDefinition.VictoryRule"/> や <see cref="CTeamDefinition.RoundEndDefinition"/> に残します。
/// </remarks>
public sealed class CTeamProfileVictoryDefinition
{
    /// <summary>
    /// Group 勝利定義を作成します。
    /// </summary>
    /// <param name="group">勝利する Group。</param>
    /// <param name="debugName">ログや調査用の識別名。</param>
    /// <param name="winnerTeam">終了処理に渡す勝者チーム。</param>
    /// <param name="priority">評価順。小さい値ほど先に評価されます。</param>
    /// <param name="specificReason">特殊終了理由。null の場合は勝者チームの標準終了定義を使います。</param>
    /// <param name="requiresVanillaEndLock">この Group の生存者がいる間、vanilla の終了判定を止めるか。</param>
    /// <param name="isEnabled">追加の有効条件。</param>
    /// <param name="profileKey">所属 Profile。通常は <see cref="CTeamProfileDefinition"/> から自動設定されます。</param>
    public CTeamProfileVictoryDefinition(
        CTeamGroup group,
        string debugName,
        CTeam winnerTeam,
        int priority,
        string? specificReason = null,
        bool requiresVanillaEndLock = false,
        Func<RoundVictoryContext, bool>? isEnabled = null,
        string? profileKey = null)
    {
        Group = group;
        DebugName = debugName;
        WinnerTeam = winnerTeam;
        Priority = priority;
        SpecificReason = specificReason;
        RequiresVanillaEndLock = requiresVanillaEndLock;
        IsEnabled = isEnabled;
        ProfileKey = profileKey;
    }

    /// <summary>
    /// 勝利する Group。
    /// </summary>
    public CTeamGroup Group { get; }

    /// <summary>
    /// ログや調査用の識別名。
    /// </summary>
    public string DebugName { get; }

    /// <summary>
    /// 終了処理に渡す勝者チーム。
    /// </summary>
    public CTeam WinnerTeam { get; }

    /// <summary>
    /// 評価順。小さい値ほど先に評価されます。
    /// </summary>
    public int Priority { get; }

    /// <summary>
    /// 特殊終了理由。null の場合は勝者チームの標準終了定義を使います。
    /// </summary>
    public string? SpecificReason { get; }

    /// <summary>
    /// この Group の生存者がいる間、vanilla の終了判定を止めるか。
    /// </summary>
    public bool RequiresVanillaEndLock { get; }

    /// <summary>
    /// 追加の有効条件。
    /// </summary>
    public Func<RoundVictoryContext, bool>? IsEnabled { get; }

    /// <summary>
    /// 所属 Profile。
    /// </summary>
    public string? ProfileKey { get; }

    /// <summary>
    /// ProfileKey を付与した勝利定義を返します。
    /// </summary>
    public CTeamProfileVictoryDefinition WithProfile(string profileKey) =>
        new(Group, DebugName, WinnerTeam, Priority, SpecificReason, RequiresVanillaEndLock, IsEnabled, profileKey);

    /// <summary>
    /// 指定チームがこの勝利 Group に解決されるかを返します。
    /// </summary>
    public bool CanRepresentTeam(CTeam team) =>
        CTeamProfileRegistry.GetGroup(team, ProfileKey) == Group;

    /// <summary>
    /// ラウンド勝利判定で使う Rule へ変換します。
    /// </summary>
    public RoundVictoryRule ToVictoryRule()
    {
        return RoundVictoryRule.ForTeamSide(
            WinnerTeam,
            Priority,
            CTeamProfileRegistry.GetTeams(Group, ProfileKey),
            DebugName,
            SpecificReason,
            RequiresVanillaEndLock,
            IsProfileEnabled);
    }

    private bool IsProfileEnabled(RoundVictoryContext context)
    {
        if (!string.Equals(context.TeamProfile, ProfileKey, StringComparison.Ordinal))
            return false;

        return IsEnabled?.Invoke(context) ?? true;
    }
}

/// <summary>
/// Profile 定義の提供元。
/// </summary>
public interface ICTeamProfileDefinitionSource
{
    IEnumerable<CTeamProfileDefinition> GetDefinitions();
}

/// <summary>
/// Profile 定義ソースの基底クラス。
/// </summary>
public abstract class CTeamProfileDefinitionSource : ICTeamProfileDefinitionSource
{
    public abstract IEnumerable<CTeamProfileDefinition> GetDefinitions();

    /// <summary>
    /// Profile 定義を作成します。<paramref name="teamGroups"/> は全 <see cref="CTeam"/> 分を埋めます。
    /// </summary>
    protected static CTeamProfileDefinition Define(
        string profileKey,
        IEnumerable<CTeamProfileVictoryDefinition> victories,
        params CTeamProfileTeamGroup[] teamGroups) =>
        new(profileKey, teamGroups, victories);

    /// <summary>
    /// Profile 内のチーム割当行を作成します。
    /// </summary>
    protected static CTeamProfileTeamGroup Map(CTeam team, CTeamGroup group) =>
        new(team, group);

    /// <summary>
    /// Profile 内の Group 勝利定義を作成します。
    /// </summary>
    protected static CTeamProfileVictoryDefinition Victory(
        CTeamGroup group,
        string debugName,
        CTeam winnerTeam,
        int priority,
        string? specificReason = null,
        bool requiresVanillaEndLock = false,
        Func<RoundVictoryContext, bool>? isEnabled = null) =>
        new(group, debugName, winnerTeam, priority, specificReason, requiresVanillaEndLock, isEnabled);
}

/// <summary>
/// 現在の Profile と Profile 定義を解決するレジストリ。
/// </summary>
public static class CTeamProfileRegistry
{
    private static readonly Lazy<IReadOnlyDictionary<string, CTeamProfileDefinition>> Definitions =
        new(BuildDefinitions);

    /// <summary>
    /// 登録済み Profile 定義一覧。
    /// </summary>
    public static IReadOnlyCollection<CTeamProfileDefinition> All => Definitions.Value.Values.ToList();

    /// <summary>
    /// Profile 定義を取得します。未指定の場合は現在の active Profile を使います。
    /// </summary>
    public static CTeamProfileDefinition Get(string? profileKey = null)
    {
        profileKey ??= CTeamProfileManager.ActiveProfile;

        if (Definitions.Value.TryGetValue(profileKey, out var definition))
            return definition;

        if (Definitions.Value.TryGetValue(CTeamProfileManager.DefaultProfile, out var defaultDefinition))
            return defaultDefinition;

        throw new ArgumentOutOfRangeException(nameof(profileKey), profileKey, null);
    }

    /// <summary>
    /// 指定チームの現在 Profile 内 Group を取得します。
    /// </summary>
    /// <remarks>
    /// 未定義の場合は <see cref="CTeamGroup.Undefined"/> を返します。
    /// </remarks>
    public static CTeamGroup GetGroup(CTeam team, string? profileKey = null)
    {
        return TryGetGroup(team, out var group, profileKey)
            ? group
            : CTeamGroup.Undefined;
    }

    /// <summary>
    /// 指定チームの現在 Profile 内 Group を取得します。
    /// </summary>
    public static bool TryGetGroup(CTeam team, out CTeamGroup group, string? profileKey = null)
    {
        var definition = Get(profileKey);
        if (definition.TryGetGroup(team, out group))
            return true;

        group = CTeamGroup.Undefined;
        return false;
    }

    /// <summary>
    /// 指定 Group に所属するチーム一覧を取得します。
    /// </summary>
    public static IReadOnlyCollection<CTeam> GetTeams(CTeamGroup group, string? profileKey = null)
    {
        var definition = Get(profileKey);
        return definition.GetTeams(group);
    }

    /// <summary>
    /// 指定 Profile の Group 勝利定義一覧を取得します。
    /// </summary>
    public static IReadOnlyCollection<CTeamProfileVictoryDefinition> GetVictories(string? profileKey = null)
    {
        var definition = Get(profileKey);
        return definition.Victories.Values.ToList();
    }

    /// <summary>
    /// 指定 Group の勝利定義を取得します。
    /// </summary>
    public static CTeamProfileVictoryDefinition GetVictory(CTeamGroup group, string? profileKey = null)
    {
        var definition = Get(profileKey);
        if (definition.TryGetVictory(group, out var victory))
            return victory;

        throw new ArgumentOutOfRangeException(nameof(group), group, null);
    }

    /// <summary>
    /// 指定チームを現在 Profile の Group 勝者チームへ解決します。
    /// </summary>
    /// <remarks>
    /// チームが <see cref="CTeamGroup.Undefined"/> または勝利定義を持たない Group に属する場合、
    /// 元のチームをそのまま返します。
    /// </remarks>
    public static CTeam ResolveWinnerTeam(CTeam requestedTeam, string? profileKey = null)
    {
        return TryResolveWinnerTeam(requestedTeam, out var winnerTeam, profileKey)
            ? winnerTeam
            : requestedTeam;
    }

    /// <summary>
    /// 指定チームを現在 Profile の Group 勝者チームへ解決します。
    /// </summary>
    public static bool TryResolveWinnerTeam(CTeam requestedTeam, out CTeam winnerTeam, string? profileKey = null)
    {
        var definition = Get(profileKey);
        var victory = definition.Victories.Values
            .OrderBy(candidate => candidate.Priority)
            .FirstOrDefault(candidate => candidate.CanRepresentTeam(requestedTeam));

        if (victory == null)
        {
            winnerTeam = requestedTeam;
            return false;
        }

        winnerTeam = victory.WinnerTeam;
        return true;
    }

    /// <summary>
    /// Profile 定義を読み込み、Profile重複とチーム割当漏れを検証します。
    /// </summary>
    private static IReadOnlyDictionary<string, CTeamProfileDefinition> BuildDefinitions()
    {
        var definitions = DefinitionSourceLoader
            .CreateInstances<ICTeamProfileDefinitionSource>()
            .SelectMany(source => source.GetDefinitions())
            .ToList();

        var duplicateProfile = definitions
            .GroupBy(definition => definition.ProfileKey)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateProfile != null)
            throw new InvalidOperationException($"Duplicate CTeamProfile definition: {duplicateProfile.Key}");

        var allTeams = Enum.GetValues(typeof(CTeam)).Cast<CTeam>().ToList();
        var missingTeamProfile = definitions
            .Select(definition => new
            {
                definition.ProfileKey,
                MissingTeams = allTeams
                    .Where(team => !definition.TeamGroups.ContainsKey(team))
                    .ToList()
            })
            .FirstOrDefault(profile => profile.MissingTeams.Count > 0);

        if (missingTeamProfile != null)
            throw new InvalidOperationException(
                $"CTeamProfile definition has missing teams: {missingTeamProfile.ProfileKey}/{string.Join(", ", missingTeamProfile.MissingTeams)}");

        return definitions.ToDictionary(definition => definition.ProfileKey);
    }
}
