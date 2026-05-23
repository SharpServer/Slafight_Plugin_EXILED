using System.Collections.Generic;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.API.Features.Teams;

public static class CTeamPlayerState
{
    private static readonly Dictionary<int, CTeam> TeamOverrides = new();

    public static void SetTeamOverride(Player player, CTeam team)
    {
        if (player == null)
            return;

        TeamOverrides[player.Id] = team;
    }

    public static bool TryGetTeamOverride(Player player, out CTeam team)
    {
        team = CTeam.Null;
        return player != null && TeamOverrides.TryGetValue(player.Id, out team);
    }

    public static void ClearTeamOverride(Player player)
    {
        if (player != null)
            TeamOverrides.Remove(player.Id);
    }

    public static void ClearAllTeamOverrides()
    {
        TeamOverrides.Clear();
    }
}
