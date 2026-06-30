using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using LabApi.Events.Arguments.PlayerEvents;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.Extensions;
using PlayerHandlers = Exiled.Events.Handlers.Player;
using ServerHandlers = Exiled.Events.Handlers.Server;
using LabPlayerEvents = LabApi.Events.Handlers.PlayerEvents;

namespace Slafight_Plugin_EXILED.API.Features;

public class KillCounter : IBootstrapHandler
{
    private static readonly Dictionary<int, int> RoundKillCounts = new();
    private static readonly Dictionary<int, int> RoleSessionKillCounts = new();
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
            return;

        PlayerHandlers.Died += OnDied;
        PlayerHandlers.Left += OnLeft;
        ServerHandlers.RoundStarted += ResetAll;
        ServerHandlers.RestartingRound += ResetAll;
        ServerHandlers.WaitingForPlayers += ResetAll;
        LabPlayerEvents.ChangedRole += OnChangedRole;

        _registered = true;
    }

    public static void Unregister()
    {
        if (!_registered)
            return;

        PlayerHandlers.Died -= OnDied;
        PlayerHandlers.Left -= OnLeft;
        ServerHandlers.RoundStarted -= ResetAll;
        ServerHandlers.RestartingRound -= ResetAll;
        ServerHandlers.WaitingForPlayers -= ResetAll;
        LabPlayerEvents.ChangedRole -= OnChangedRole;

        ResetAll();
        _registered = false;
    }

    public static int GetRoundKills(Player? player)
        => TryGetPlayerId(player, out var playerId) ? GetRoundKills(playerId) : 0;

    public static int GetRoundKills(int playerId)
        => RoundKillCounts.TryGetValue(playerId, out var count) ? count : 0;

    public static int GetRoleSessionKills(Player? player)
        => TryGetPlayerId(player, out var playerId) ? GetRoleSessionKills(playerId) : 0;

    public static int GetRoleSessionKills(int playerId)
        => RoleSessionKillCounts.TryGetValue(playerId, out var count) ? count : 0;

    public static int GetTotalRoundKills()
        => RoundKillCounts.Values.Sum();

    public static IReadOnlyDictionary<int, int> GetRoundSnapshot()
        => new Dictionary<int, int>(RoundKillCounts);

    public static IReadOnlyDictionary<int, int> GetRoleSessionSnapshot()
        => new Dictionary<int, int>(RoleSessionKillCounts);

    public static void ResetRoleSession(Player? player)
    {
        if (TryGetPlayerId(player, out var playerId))
            ResetRoleSession(playerId);
    }

    public static void ResetRoleSession(int playerId)
        => RoleSessionKillCounts.Remove(playerId);

    public static void ClearPlayer(Player? player)
    {
        if (!TryGetPlayerId(player, out var playerId))
            return;

        RoundKillCounts.Remove(playerId);
        RoleSessionKillCounts.Remove(playerId);
    }

    public static void ResetAll()
    {
        RoundKillCounts.Clear();
        RoleSessionKillCounts.Clear();
    }

    public static bool TryRecordKill(Player? attacker, Player? target)
    {
        if (!IsCountablePlayer(attacker) || !IsCountablePlayer(target))
            return false;

        if (attacker!.Id == target!.Id)
            return false;

        AddKill(attacker.Id);
        return true;
    }

    private static void OnDied(DiedEventArgs ev)
        => TryRecordKill(ev.Attacker, ev.Player);

    private static void OnLeft(LeftEventArgs ev)
        => ClearPlayer(ev.Player);

    private static void OnChangedRole(PlayerChangedRoleEventArgs ev)
        => ResetRoleSession(ev.Player?.PlayerId ?? 0);

    private static void AddKill(int playerId)
    {
        RoundKillCounts[playerId] = GetRoundKills(playerId) + 1;
        RoleSessionKillCounts[playerId] = GetRoleSessionKills(playerId) + 1;
    }

    private static bool TryGetPlayerId(Player? player, out int playerId)
    {
        playerId = 0;

        try
        {
            if (player?.ReferenceHub == null)
                return false;

            playerId = player.Id;
            return playerId > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsCountablePlayer(Player? player)
    {
        try
        {
            return TryGetPlayerId(player, out _) &&
                   player != null &&
                   player.IsNotHost();
        }
        catch
        {
            return false;
        }
    }
}
