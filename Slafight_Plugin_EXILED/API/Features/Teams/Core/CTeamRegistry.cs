using System;
using System.Collections.Generic;
using System.Linq;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.Common;
using Slafight_Plugin_EXILED.API.Features.RoundVictory.Core;

namespace Slafight_Plugin_EXILED.API.Features.Teams.Core;

public interface ICTeamDefinitionSource
{
    IEnumerable<CTeamDefinition> GetDefinitions();
}

public abstract class CTeamDefinitionSource : ICTeamDefinitionSource
{
    public abstract IEnumerable<CTeamDefinition> GetDefinitions();

    protected static CTeamDefinition Define(
        CTeam team,
        string name,
        string cassie,
        string color,
        bool isGoI,
        RoundVictoryRule? victoryRule = null,
        RoundEndDefinition? roundEndDefinition = null) =>
        new(team, name, cassie, color, isGoI, victoryRule, roundEndDefinition);
}

/// <summary>
/// <see cref="CTeam"/> の静的定義を管理します。
/// </summary>
public static class CTeamRegistry
{
    private static readonly Lazy<IReadOnlyDictionary<CTeam, CTeamDefinition>> Definitions =
        new(BuildDefinitions);

    /// <summary>
    /// 指定された <see cref="CTeam"/> の定義を取得します。
    /// </summary>
    /// <param name="team">取得対象のチーム。</param>
    /// <returns>チームに対応する定義。</returns>
    /// <exception cref="ArgumentOutOfRangeException">未定義の <see cref="CTeam"/> が指定された場合。</exception>
    public static CTeamDefinition Get(CTeam team)
    {
        if (Definitions.Value.TryGetValue(team, out var definition))
            return definition;

        throw new ArgumentOutOfRangeException(nameof(team), team, null);
    }

    public static IReadOnlyCollection<CTeamDefinition> All => Definitions.Value.Values.ToList();

    private static IReadOnlyDictionary<CTeam, CTeamDefinition> BuildDefinitions()
    {
        var definitions = DefinitionSourceLoader
            .CreateInstances<ICTeamDefinitionSource>()
            .SelectMany(source => source.GetDefinitions())
            .ToList();

        var duplicate = definitions
            .GroupBy(definition => definition.Team)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate != null)
            throw new InvalidOperationException($"Duplicate CTeam definition: {duplicate.Key}");

        return definitions.ToDictionary(definition => definition.Team);
    }
}
