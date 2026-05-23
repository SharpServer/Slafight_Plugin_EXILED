using System;
using System.Collections.Generic;
using System.Linq;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Features.RoundVictory;

namespace Slafight_Plugin_EXILED.API.Features.Teams;

public readonly struct CTeamProfileTeamGroup
{
    public CTeamProfileTeamGroup(CTeam team, CTeamGroup group)
    {
        Team = team;
        Group = group;
    }

    public CTeam Team { get; }
    public CTeamGroup Group { get; }
}

public sealed class CTeamProfileDefinition
{
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

    public string ProfileKey { get; }
    public IReadOnlyDictionary<CTeam, CTeamGroup> TeamGroups { get; }
    public IReadOnlyDictionary<CTeamGroup, CTeamProfileVictoryDefinition> Victories { get; }

    public bool TryGetGroup(CTeam team, out CTeamGroup group) =>
        TeamGroups.TryGetValue(team, out group);

    public bool TryGetVictory(CTeamGroup group, out CTeamProfileVictoryDefinition victory) =>
        Victories.TryGetValue(group, out victory);

    public IReadOnlyCollection<CTeam> GetTeams(CTeamGroup group) =>
        TeamGroups
            .Where(pair => pair.Value == group)
            .Select(pair => pair.Key)
            .ToList();
}

public sealed class CTeamProfileVictoryDefinition
{
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

    public CTeamGroup Group { get; }
    public string DebugName { get; }
    public CTeam WinnerTeam { get; }
    public int Priority { get; }
    public string? SpecificReason { get; }
    public bool RequiresVanillaEndLock { get; }
    public Func<RoundVictoryContext, bool>? IsEnabled { get; }
    public string? ProfileKey { get; }

    public CTeamProfileVictoryDefinition WithProfile(string profileKey) =>
        new(Group, DebugName, WinnerTeam, Priority, SpecificReason, RequiresVanillaEndLock, IsEnabled, profileKey);

    public bool CanRepresentTeam(CTeam team) =>
        CTeamProfileRegistry.GetGroup(team, ProfileKey) == Group;

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

public interface ICTeamProfileDefinitionSource
{
    IEnumerable<CTeamProfileDefinition> GetDefinitions();
}

public abstract class CTeamProfileDefinitionSource : ICTeamProfileDefinitionSource
{
    public abstract IEnumerable<CTeamProfileDefinition> GetDefinitions();

    protected static CTeamProfileDefinition Define(
        string profileKey,
        IEnumerable<CTeamProfileVictoryDefinition> victories,
        params CTeamProfileTeamGroup[] teamGroups) =>
        new(profileKey, teamGroups, victories);

    protected static CTeamProfileTeamGroup Map(CTeam team, CTeamGroup group) =>
        new(team, group);

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

public static class CTeamProfileRegistry
{
    private static readonly Lazy<IReadOnlyDictionary<string, CTeamProfileDefinition>> Definitions =
        new(BuildDefinitions);

    public static IReadOnlyCollection<CTeamProfileDefinition> All => Definitions.Value.Values.ToList();

    public static CTeamProfileDefinition Get(string? profileKey = null)
    {
        profileKey ??= CTeamProfileManager.ActiveProfile;

        if (Definitions.Value.TryGetValue(profileKey, out var definition))
            return definition;

        if (Definitions.Value.TryGetValue(CTeamProfileManager.DefaultProfile, out var defaultDefinition))
            return defaultDefinition;

        throw new ArgumentOutOfRangeException(nameof(profileKey), profileKey, null);
    }

    public static CTeamGroup GetGroup(CTeam team, string? profileKey = null)
    {
        return TryGetGroup(team, out var group, profileKey)
            ? group
            : CTeamGroup.Undefined;
    }

    public static bool TryGetGroup(CTeam team, out CTeamGroup group, string? profileKey = null)
    {
        var definition = Get(profileKey);
        if (definition.TryGetGroup(team, out group))
            return true;

        group = CTeamGroup.Undefined;
        return false;
    }

    public static IReadOnlyCollection<CTeam> GetTeams(CTeamGroup group, string? profileKey = null)
    {
        var definition = Get(profileKey);
        return definition.GetTeams(group);
    }

    public static IReadOnlyCollection<CTeamProfileVictoryDefinition> GetVictories(string? profileKey = null)
    {
        var definition = Get(profileKey);
        return definition.Victories.Values.ToList();
    }

    public static CTeamProfileVictoryDefinition GetVictory(CTeamGroup group, string? profileKey = null)
    {
        var definition = Get(profileKey);
        if (definition.TryGetVictory(group, out var victory))
            return victory;

        throw new ArgumentOutOfRangeException(nameof(group), group, null);
    }

    public static CTeam ResolveWinnerTeam(CTeam requestedTeam, string? profileKey = null)
    {
        return TryResolveWinnerTeam(requestedTeam, out var winnerTeam, profileKey)
            ? winnerTeam
            : requestedTeam;
    }

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
