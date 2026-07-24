using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.Teams.Core;
using Slafight_Plugin_EXILED.API.Features.Teams.Profiles;

namespace Slafight_Plugin_EXILED.Extensions;

public struct CustomTeamInfo
{
    public CTeam Team;
    public CTeamGroup Group;
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
            Group = CTeamProfileRegistry.GetGroup(team),
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

    public static CTeamGroup GetTeamGroup(this CTeam team)
    {
        return CTeamProfileRegistry.GetGroup(team);
    }

    public static void SetTeamOverride(this Player player, CTeam team)
    {
        CTeamPlayerState.SetTeamOverride(player, team);
    }

    public static void ClearTeamOverride(this Player player)
    {
        CTeamPlayerState.ClearTeamOverride(player);
    }
}
