using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.API.Features.Teams;

public readonly struct CTeamEscapeTarget
{
    public CTeamEscapeTarget(RoleTypeId? vanilla, CRoleTypeId? custom)
    {
        Vanilla = vanilla;
        Custom = custom;
    }

    public RoleTypeId? Vanilla { get; }
    public CRoleTypeId? Custom { get; }
    public bool IsEmpty => Vanilla == null && Custom == null;

    public static CTeamEscapeTarget None => new(null, null);
    public static CTeamEscapeTarget VanillaRole(RoleTypeId role) => new(role, null);
    public static CTeamEscapeTarget CustomRole(CRoleTypeId role) => new(null, role);
}

public sealed class CTeamEscapeContext
{
    public CTeamEscapeContext(Player player)
    {
        Player = player;
        PlayerTeam = player.GetTeam();
        CufferTeam = player.Cuffer?.GetTeam() ?? CTeam.Null;
        TeamProfile = CTeamProfileManager.ActiveProfile;
    }

    public Player Player { get; }
    public CTeam PlayerTeam { get; }
    public CTeam CufferTeam { get; }
    public string TeamProfile { get; }
}

public sealed class CTeamEscapeRule
{
    public CTeamEscapeRule(
        string debugName,
        int priority,
        Func<CTeamEscapeContext, bool> matches,
        CTeamEscapeTarget target)
    {
        DebugName = debugName;
        Priority = priority;
        Matches = matches;
        Target = target;
    }

    public string DebugName { get; }
    public int Priority { get; }
    public Func<CTeamEscapeContext, bool> Matches { get; }
    public CTeamEscapeTarget Target { get; }
}

public interface ICTeamEscapeRuleSource
{
    IEnumerable<CTeamEscapeRule> GetRules();
}

public abstract class CTeamEscapeRuleSource : ICTeamEscapeRuleSource
{
    public abstract IEnumerable<CTeamEscapeRule> GetRules();

    protected static CTeamEscapeRule Define(
        string debugName,
        int priority,
        Func<CTeamEscapeContext, bool> matches,
        CTeamEscapeTarget target) =>
        new(debugName, priority, matches, target);

    protected static CTeamEscapeRule DefineTeamRule(
        string debugName,
        int priority,
        CTeam playerTeam,
        IEnumerable<CTeam>? cufferTeams,
        CTeamEscapeTarget target)
    {
        var cuffers = cufferTeams != null ? new HashSet<CTeam>(cufferTeams) : null;
        return Define(
            debugName,
            priority,
            context => context.PlayerTeam == playerTeam &&
                       (cuffers == null || cuffers.Contains(context.CufferTeam)),
            target);
    }

    protected static CTeamEscapeRule DefineProfileRule(
        string debugName,
        int priority,
        string profileKey,
        Func<CTeamEscapeContext, bool> matches,
        CTeamEscapeTarget target) =>
        Define(
            debugName,
            priority,
            context => string.Equals(context.TeamProfile, profileKey, StringComparison.Ordinal) &&
                       matches(context),
            target);
}

public static class CTeamEscapeRegistry
{
    private static readonly Lazy<IReadOnlyList<CTeamEscapeRule>> Rules =
        new(BuildRules);

    private static readonly List<Func<CTeamEscapeContext, CTeamEscapeTarget?>> DynamicOverrides = new();

    public static void AddDynamicOverride(Func<CTeamEscapeContext, CTeamEscapeTarget?> rule)
    {
        if (rule != null)
            DynamicOverrides.Add(rule);
    }

    public static void ClearDynamicOverrides()
    {
        DynamicOverrides.Clear();
    }

    public static CTeamEscapeTarget Resolve(Player player)
    {
        if (player == null)
            return CTeamEscapeTarget.None;

        var context = new CTeamEscapeContext(player);

        foreach (var rule in DynamicOverrides)
        {
            var target = rule(context);
            if (target is { IsEmpty: false } resolved)
                return resolved;
        }

        return Rules.Value
            .OrderBy(rule => rule.Priority)
            .FirstOrDefault(rule => rule.Matches(context))
            ?.Target ?? CTeamEscapeTarget.None;
    }

    private static IReadOnlyList<CTeamEscapeRule> BuildRules()
    {
        return DefinitionSourceLoader
            .CreateInstances<ICTeamEscapeRuleSource>()
            .SelectMany(source => source.GetRules())
            .ToList();
    }
}
