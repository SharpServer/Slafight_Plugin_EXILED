using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.Teams.Profiles;

namespace Slafight_Plugin_EXILED.API.Features.RoundVictory.Core;

public static class EvacuationRoundEndState
{
    private static readonly List<CTeam> EscapedTeamOrder = [];
    private static readonly HashSet<CTeam> EscapedTeams = [];

    public static bool IsActive { get; private set; }
    public static bool HasEscapedPlayers => EscapedTeamOrder.Count > 0;

    public static void Begin()
    {
        IsActive = true;
        ClearEscapes();
    }

    public static void End()
    {
        IsActive = false;
        ClearEscapes();
    }

    public static void Reset() => End();

    public static void RecordEscape(CTeam team)
    {
        if (!IsActive || team == CTeam.Null)
            return;

        EscapedTeamOrder.Add(team);
        EscapedTeams.Add(team);
    }

    public static bool ShouldDeferRoundEnd(IReadOnlyList<Player> alivePlayers)
    {
        return IsActive &&
               HasEscapedPlayers &&
               alivePlayers.Any(RoundVictoryDefinitions.IsAliveRoundPlayer);
    }

    public static bool TryCreateAllEscapedResult(out RoundVictoryResult result)
    {
        result = RoundVictoryResult.None;

        if (!IsActive || !HasEscapedPlayers)
            return false;

        if (!TryResolveEscapedWinner(out var winnerTeam, out var specificReason))
            return false;

        result = RoundVictoryResult.ForTeam("EvacuationAllEscaped", winnerTeam, specificReason);
        return true;
    }

    private static bool TryResolveEscapedWinner(out CTeam winnerTeam, out string? specificReason)
    {
        var activeProfile = CTeamProfileManager.ActiveProfile;
        var escapedGroups = EscapedTeams
            .Select(team => CTeamProfileRegistry.GetGroup(team, activeProfile))
            .Where(group => group is not CTeamGroup.Null and not CTeamGroup.Undefined)
            .Distinct()
            .ToList();

        if (escapedGroups.Count == 1 &&
            TryGetGroupVictory(escapedGroups[0], activeProfile, out winnerTeam, out specificReason))
        {
            return true;
        }

        foreach (var team in EscapedTeamOrder)
        {
            var group = CTeamProfileRegistry.GetGroup(team, activeProfile);
            if (group is not CTeamGroup.Null and not CTeamGroup.Undefined &&
                TryGetGroupVictory(group, activeProfile, out winnerTeam, out specificReason))
            {
                return true;
            }

            winnerTeam = team;
            specificReason = null;
            return true;
        }

        winnerTeam = CTeam.Null;
        specificReason = null;
        return false;
    }

    private static bool TryGetGroupVictory(
        CTeamGroup group,
        string activeProfile,
        out CTeam winnerTeam,
        out string? specificReason)
    {
        try
        {
            var victory = CTeamProfileRegistry.GetVictory(group, activeProfile);
            winnerTeam = victory.WinnerTeam;
            specificReason = victory.SpecificReason;
            return true;
        }
        catch
        {
            winnerTeam = CTeam.Null;
            specificReason = null;
            return false;
        }
    }

    private static void ClearEscapes()
    {
        EscapedTeamOrder.Clear();
        EscapedTeams.Clear();
    }
}
