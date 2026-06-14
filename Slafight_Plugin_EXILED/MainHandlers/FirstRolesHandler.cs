using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.API.Objects;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.MainHandlers;

public class FirstRolesHandler : IBootstrapHandler, System.IDisposable
{
    public static FirstRolesHandler Instance { get; private set; }

    public static void Register()
    {
        Unregister();
        Instance = new FirstRolesHandler();
    }

    public static void Unregister()
    {
        Instance?.Dispose();
        Instance = null;
    }

    private static readonly HashSet<int> FirstRoleAssignedPlayerIds = [];

    private readonly List<int> _roundStartSpawnQueue = [];
    private readonly HashSet<int> _roundStartQueuedIds = [];

    private int _assignedCount;
    private bool _disposed;
    private bool _roundLockedByFirstRoles;
    private CoroutineHandle _coroutineHandle;

    public FirstRolesHandler()
    {
        Exiled.Events.Handlers.Server.WaitingForPlayers += OnWaitingForPlayers;
        Exiled.Events.Handlers.Server.RestartingRound += OnRestartingRound;
        Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;
        Exiled.Events.Handlers.Player.Spawned += OnPlayerSpawned;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        Exiled.Events.Handlers.Server.WaitingForPlayers -= OnWaitingForPlayers;
        Exiled.Events.Handlers.Server.RestartingRound -= OnRestartingRound;
        Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;
        Exiled.Events.Handlers.Player.Spawned -= OnPlayerSpawned;

        ResetRoundState("Dispose", unlockRound: true);
        System.GC.SuppressFinalize(this);
    }

    private void OnWaitingForPlayers()
    {
        ResetRoundState("WaitingForPlayers", unlockRound: true);
    }

    private void OnRestartingRound()
    {
        ResetRoundState("RestartingRound", unlockRound: true);
    }

    private void ResetRoundState(string reason, bool unlockRound)
    {
        Log.Debug($"[FirstRoles] {reason}: cleaning up state.");

        if (_coroutineHandle.IsRunning)
            Timing.KillCoroutines(_coroutineHandle);

        _roundStartSpawnQueue.Clear();
        _roundStartQueuedIds.Clear();
        _assignedCount = 0;
        _coroutineHandle = default;
        FirstRoleAssignedPlayerIds.Clear();
        RoleSyncReadiness.Clear();

        if (unlockRound)
            UnlockRound($"{reason} cleanup");

        Log.Debug($"[FirstRoles] {reason}: state cleaned up.");
    }

    private void OnRoundStarted()
    {
        if (_coroutineHandle.IsRunning)
            Timing.KillCoroutines(_coroutineHandle);

        _assignedCount = 0;

        _LimitChecker();
        OnAssign();

        Log.Debug($"[FirstRoles] RoundStarted: scheduling assignment (queued={_roundStartSpawnQueue.Count}).");

        LockRound();
        _coroutineHandle = Timing.RunCoroutine(RoundStartAssignCoroutine());
    }

    private IEnumerator<float> RoundStartAssignCoroutine()
    {
        Log.Debug("[FirstRoles] RoundStartAssignCoroutine: started");

        try
        {
            CollectUnassignedPlayersFromList();
            PruneRoundStartQueue();
            var roleSyncWaiter = WaitForQueuedRoleSyncReadiness();
            while (roleSyncWaiter.MoveNext())
                yield return roleSyncWaiter.Current;

            PruneRoundStartQueue();

            if (_roundStartSpawnQueue.Count == 0)
            {
                Log.Debug("[FirstRoles] No players to assign roles.");
                yield break;
            }

            var shuffled = _roundStartSpawnQueue
                .Select(Player.Get)
                .Where(IsEligibleForFirstRole)
                .Where(IsFirstRoleUnassigned)
                .OrderBy(_ => UnityEngine.Random.value)
                .ToList();

            if (shuffled.Count == 0)
            {
                Log.Debug("[FirstRoles] No eligible players after queue refresh.");
                yield break;
            }

            int total = shuffled.Count;
            int scpCount = Mathf.Max(1, total / 5);

            Log.Debug($"[FirstRoles] Assigning roles: total={total}, SCP={scpCount}");

            for (int i = 0; i < shuffled.Count; i++)
            {
                var target = shuffled[i];
                if (!IsEligibleForFirstRole(target))
                {
                    Log.Debug($"[FirstRoles] Skip ineligible target at index {i}");
                    continue;
                }

                if (!IsFirstRoleUnassigned(target))
                {
                    Log.Debug($"[FirstRoles] Already assigned, skip: {target.Nickname}");
                    continue;
                }

                bool isScp = i < scpCount;
                var table = isScp
                    ? RoleTables.GetScpRoles()
                    : GetHumanRoleTable(_assignedCount);

                if (!TryAssignRole(target, table, RoleSpawnFlags.All))
                    continue;

                _assignedCount++;
                Log.Debug($"[FirstRoles] Assigned role #{_assignedCount}/{total}: {target.Nickname} (isScp={isScp})");
            }

            Log.Debug("[FirstRoles] All roles assigned (coroutine end).");
        }
        finally
        {
            _coroutineHandle = default;
            UnlockRound("assignment finished");
        }
    }

    private void OnPlayerSpawned(Exiled.Events.EventArgs.Player.SpawnedEventArgs ev)
    {
        var player = ev.Player;
        if (!IsEligibleForFirstRole(player))
            return;

        if (ev.Reason != SpawnReason.RoundStart)
        {
            Log.Debug($"[FirstRoles] Spawned skipped: Reason != RoundStart ({ev.Reason})");
            return;
        }

        if (!IsFirstRoleUnassigned(player))
        {
            Log.Debug($"[FirstRoles] Spawned skipped: already assigned ({player.Nickname})");
            return;
        }

        if (player.IsNPC)
        {
            int playerId = player.Id;
            Timing.CallDelayed(RoleSpawnTimings.FirstRolesNpcApplyRetry, () =>
            {
                var current = Player.Get(playerId);
                if (!IsEligibleForFirstRole(current) || !IsFirstRoleUnassigned(current))
                    return;

                QueueRoundStartPlayer(current, "delayed NPC");
            });

            return;
        }

        QueueRoundStartPlayer(player, "Spawned(RoundStart)");
    }

    private IEnumerator<float> WaitForQueuedRoleSyncReadiness()
    {
        float waited = 0f;
        bool loggedWait = false;

        while (waited < RoleSpawnTimings.FirstRolesSyncReadyTimeout &&
               HasQueuedPlayersWaitingForRoleSync(out int waitingCount))
        {
            if (!loggedWait)
            {
                loggedWait = true;
                Log.Debug($"[FirstRoles] Waiting for vanilla role sync settle: players={waitingCount}");
            }

            yield return Timing.WaitForSeconds(RoleSpawnTimings.FirstRolesSyncReadyPollInterval);
            waited += RoleSpawnTimings.FirstRolesSyncReadyPollInterval;
        }

        if (HasQueuedPlayersWaitingForRoleSync(out int remainingCount))
            Log.Warn($"[FirstRoles] Vanilla role sync did not settle for {remainingCount} player(s) before timeout; continuing with guarded assignment.");
    }

    private bool HasQueuedPlayersWaitingForRoleSync(out int waitingCount)
    {
        waitingCount = 0;

        foreach (int playerId in _roundStartSpawnQueue)
        {
            var player = Player.Get(playerId);
            if (!IsEligibleForFirstRole(player) || !IsFirstRoleUnassigned(player))
                continue;

            if (player.IsNPC)
                continue;

            if (!RoleSyncReadiness.IsSelfRoleSyncSettled(
                    player.ReferenceHub,
                    settleSeconds: 0f))
            {
                waitingCount++;
            }
        }

        return waitingCount > 0;
    }

    private void CollectUnassignedPlayersFromList()
    {
        var players = Player.List
            .Where(IsEligibleForFirstRole)
            .Where(IsFirstRoleUnassigned)
            .ToList();

        Log.Debug($"[FirstRoles] Player.List refresh found {players.Count} unassigned players.");

        foreach (var player in players)
            QueueRoundStartPlayer(player, "Player.List refresh");
    }

    private void QueueRoundStartPlayer(Player player, string source)
    {
        if (!_roundStartQueuedIds.Add(player.Id))
        {
            Log.Debug($"[FirstRoles] {source} skipped: already in queue ({player.Nickname})");
            return;
        }

        _roundStartSpawnQueue.Add(player.Id);
        Log.Debug($"[FirstRoles] {source} collected: {player.Nickname} (queue size={_roundStartSpawnQueue.Count})");
    }

    private void PruneRoundStartQueue()
    {
        _roundStartSpawnQueue.RemoveAll(playerId =>
        {
            var player = Player.Get(playerId);
            bool remove = !IsEligibleForFirstRole(player) || !IsFirstRoleUnassigned(player);
            if (remove)
                _roundStartQueuedIds.Remove(playerId);

            return remove;
        });
    }

    private void LockRound()
    {
        Round.IsLocked = true;
        _roundLockedByFirstRoles = true;
    }

    private void UnlockRound(string reason)
    {
        if (!_roundLockedByFirstRoles)
            return;

        Round.IsLocked = false;
        _roundLockedByFirstRoles = false;
        Log.Debug($"[FirstRoles] Round unlocked ({reason}).");
    }

    private static List<WeightedRoleEntry> GetHumanRoleTable(int index)
    {
        return (index % 3) switch
        {
            0 => RoleTables.GetClassDRoles(),
            1 => RoleTables.GetScientistRoles(),
            _ => RoleTables.GetGuardRoles(),
        };
    }

    private static bool TryAssignRole(Player player, List<WeightedRoleEntry> table, RoleSpawnFlags flags)
    {
        const int maxTries = 20;
        object? choice = null;

        for (int i = 0; i < maxTries; i++)
        {
            var candidate = WeightedRole.Choose(table);
            if (candidate == null || !RoleLimitManager.CanAssign(candidate))
                continue;

            choice = candidate;
            break;
        }

        if (choice == null)
        {
            Log.Debug($"[FirstRoles] No available role for {player.Nickname} (all limited).");
            return false;
        }

        try
        {
            switch (choice)
            {
                case RoleTypeId r:
                    player.SetRole(r, flags);
                    break;
                case CRoleTypeId cr:
                    player.SetRole(cr, flags);
                    break;
                default:
                    Log.Warn($"[FirstRoles] Unsupported role choice {choice} for {player.Nickname}.");
                    return false;
            }

            RoleLimitManager.Consume(choice);
            SetFirstRoleAssigned(player);
            Log.Debug($"[FirstRoles] Assigned {choice} to {player.Nickname}.");
            return true;
        }
        catch (System.Exception ex)
        {
            Log.Error($"[FirstRoles] Failed to assign {choice} to {player.Nickname}: {ex}");
            return false;
        }
    }

    private static void _LimitChecker()
    {
        Log.Debug("[FirstRoles] _LimitChecker called.");
        RoleLimitManager.ApplyPool(RoleTables.GetCurrentLimitPool());
    }

    private static void OnAssign()
    {
        if (MapFlags.GetSeason() is SeasonTypeId.April)
            RoleTables.SetCurrentMode("April");
        else
            RoleTables.SetCurrentMode("Normal");
    }

    private static bool IsEligibleForFirstRole(Player? player)
    {
        try
        {
            return player != null &&
                   player.ReferenceHub != null &&
                   !player.IsHost &&
                   (player.IsConnected || player.IsNPC);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsFirstRoleUnassigned(Player? player) =>
        player != null && !FirstRoleAssignedPlayerIds.Contains(player.Id);

    private static void SetFirstRoleAssigned(Player player)
    {
        if (player == null)
            return;

        FirstRoleAssignedPlayerIds.Add(player.Id);
    }
}
