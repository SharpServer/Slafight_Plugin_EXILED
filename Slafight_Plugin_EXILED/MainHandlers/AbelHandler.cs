using System.Linq;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Warhead;
using MEC;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.CustomMaps.Features;
using Slafight_Plugin_EXILED.Extensions;
using PlayerRoles;
using UnityEngine;

namespace Slafight_Plugin_EXILED.MainHandlers;

public class AbelHandler : IBootstrapHandler
{
    private const float SpawnDelaySeconds = 1f;

    private static CoroutineHandle _spawnHandle;
    private static bool _registered;
    private static bool _spawnQueued;
    private static bool _roundLockApplied;

    public static void Register()
    {
        if (_registered)
            return;

        Exiled.Events.Handlers.Warhead.Detonating += OnDetonating;
        Exiled.Events.Handlers.Warhead.Detonated += OnDetonated;
        OmegaWarhead.OmegaWarheadDetonating += OnOmegaWarheadDetonating;
        _registered = true;
        Log.Debug("AbelHandler: registered warhead detonation hooks.");
    }

    public static void Unregister()
    {
        if (!_registered)
            return;

        Exiled.Events.Handlers.Warhead.Detonating -= OnDetonating;
        Exiled.Events.Handlers.Warhead.Detonated -= OnDetonated;
        OmegaWarhead.OmegaWarheadDetonating -= OnOmegaWarheadDetonating;
        if (_spawnHandle.IsRunning)
            Timing.KillCoroutines(_spawnHandle);

        ReleaseRoundLock();
        _spawnQueued = false;
        _registered = false;
        Log.Debug("AbelHandler: unregistered warhead detonation hooks.");
    }

    private static void OnDetonating(DetonatingEventArgs ev)
    {
        if (!ev.IsAllowed)
            return;

        QueuePandraBoxSpawn("Warhead.Detonating");
    }

    private static void OnDetonated()
        => QueuePandraBoxSpawn("Warhead.Detonated");

    private static void OnOmegaWarheadDetonating()
        => QueuePandraBoxSpawn("OmegaWarhead.Detonating");

    public static void QueuePandraBoxSpawn(string reason, float delay = SpawnDelaySeconds)
    {
        if (Round.IsLobby)
        {
            Log.Debug($"AbelHandler: skipped Pandra's Box spawn queue from {reason} because round is lobby.");
            return;
        }

        if (Round.InProgress && !Round.IsLocked)
        {
            Round.IsLocked = true;
            _roundLockApplied = true;
        }

        if (_spawnQueued)
        {
            Log.Debug($"AbelHandler: Pandra's Box spawn is already queued. Ignored duplicate trigger from {reason}.");
            return;
        }

        if (Random.Range(0, 3) is not 0)
        {
            return;
        }

        _spawnQueued = true;
        Log.Info($"AbelHandler: queued Pandra's Box spawn from {reason} after {delay:0.00}s.");

        if (_spawnHandle.IsRunning)
            Timing.KillCoroutines(_spawnHandle);

        _spawnHandle = Timing.CallDelayed(delay, () => ExecutePandraBoxSpawn(reason));
    }

    private static void ExecutePandraBoxSpawn(string reason)
    {
        _spawnQueued = false;

        try
        {
            if (Round.IsLobby || Round.IsEnded)
            {
                Log.Warn($"AbelHandler: skipped Pandra's Box spawn from {reason}. Lobby={Round.IsLobby}, Ended={Round.IsEnded}");
                return;
            }

            var spectators = Player.List.Count(player =>
                player.Role == RoleTypeId.Spectator &&
                player.GetCustomRole() == CRoleTypeId.None);

            Log.Info($"AbelHandler: spawning Pandra's Box from {reason}. Spectators={spectators}, RoundLocked={Round.IsLocked}");
            SpawnSystem.ForceSpawnNow(SpawnTypeId.MtfPdx);
        }
        finally
        {
            Timing.CallDelayed(2f, ReleaseRoundLock);
        }
    }

    private static void ReleaseRoundLock()
    {
        if (!_roundLockApplied)
            return;

        if (!Round.IsLobby)
            Round.IsLocked = false;

        _roundLockApplied = false;
    }
}
