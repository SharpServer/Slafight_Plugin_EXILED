using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.Teams;

namespace Slafight_Plugin_EXILED.Extensions;

public struct CustomTeamInfo
{
    public CTeam Team;
    public bool IsGoI;
    public string TeamName;
    public string CassieString;
    public string TeamColor;
}
public static class CustomTeamUtils
{
    public static CustomTeamInfo GetTeamInfo(this CTeam team)
    {
        var definition = CTeamRegistry.Get(team);
        return new CustomTeamInfo
        {
            Team = definition.Team,
            IsGoI = definition.IsGoI,
            TeamName = definition.Name,
            CassieString = definition.Cassie,
            TeamColor = definition.Color
        };
    }

    public static bool IsGoI(this CTeam team)
    {
        return CTeamRegistry.Get(team).IsGoI;
    }

    public static string GetTeamName(this CTeam team)
    {
        return CTeamRegistry.Get(team).Name;
    }

    public static string GetTeamCassie(this CTeam team)
    {
        return CTeamRegistry.Get(team).Cassie;
    }

    public static string GetTeamColor(this CTeam team)
    {
        return CTeamRegistry.Get(team).Color;
    }
}
