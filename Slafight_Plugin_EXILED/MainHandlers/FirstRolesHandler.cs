using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Objects;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

using Slafight_Plugin_EXILED.API.Interface;

namespace Slafight_Plugin_EXILED.MainHandlers;

public class FirstRolesHandler : IBootstrapHandler, System.IDisposable
{
    public static FirstRolesHandler Instance { get; private set; }
    public static void Register()
    {
        Unregister();
        Instance = new();
    }

    public static void Unregister()
    {
        Instance?.Dispose();
        Instance = null;
    }

    private bool _disposed;
    private static readonly HashSet<int> RoundStartCandidates = [];
    private static readonly Dictionary<int, PlannedInitialRole> PlannedRoles = new();
    private static readonly HashSet<int> AppliedInitialRoles = [];
    private static readonly HashSet<int> PendingNpcRoleApplies = [];
    private static CoroutineHandle _roundUnlockFallback;
    private static bool _planBuilt;
    private const int MaxNpcApplyAttempts = 8;

    private readonly struct PlannedInitialRole
    {
        public PlannedInitialRole(object role, RoleSpawnFlags spawnFlags)
        {
            Role = role;
            SpawnFlags = spawnFlags;
        }

        public object Role { get; }
        public RoleSpawnFlags SpawnFlags { get; }
    }

    public FirstRolesHandler()
    {
        Exiled.Events.Handlers.Server.WaitingForPlayers += OnWaitingForPlayers;
        Exiled.Events.Handlers.Server.RestartingRound += OnRoundRestarting;
        Exiled.Events.Handlers.Player.ChangingRole += TrackRoundStartCandidate;
        Exiled.Events.Handlers.Player.Spawned += OnPlayerSpawned;
        Exiled.Events.Handlers.Player.Left += OnPlayerLeft;
        Exiled.Events.Handlers.Server.RoundStarted += SetupRandomRoles;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Exiled.Events.Handlers.Server.WaitingForPlayers -= OnWaitingForPlayers;
        Exiled.Events.Handlers.Server.RestartingRound -= OnRoundRestarting;
        Exiled.Events.Handlers.Player.ChangingRole -= TrackRoundStartCandidate;
        Exiled.Events.Handlers.Player.Spawned -= OnPlayerSpawned;
        Exiled.Events.Handlers.Player.Left -= OnPlayerLeft;
        Exiled.Events.Handlers.Server.RoundStarted -= SetupRandomRoles;
        ResetRuntimeState(nameof(Dispose));
        System.GC.SuppressFinalize(this);
    }

    private static void OnWaitingForPlayers()
    {
        ResetRuntimeState(nameof(OnWaitingForPlayers));
        Round.IsLocked = true;
    }

    private static void OnRoundRestarting()
    {
        ResetRuntimeState(nameof(OnRoundRestarting));
    }

    private static void ResetRuntimeState(string reason)
    {
        if (_roundUnlockFallback.IsRunning)
            Timing.KillCoroutines(_roundUnlockFallback);

        RoundStartCandidates.Clear();
        PlannedRoles.Clear();
        AppliedInitialRoles.Clear();
        PendingNpcRoleApplies.Clear();
        _planBuilt = false;

        Log.Debug($"[FirstRoles] Reset runtime state ({reason})");
    }

    private static void TrackRoundStartCandidate(ChangingRoleEventArgs? ev)
    {
        if (ev?.Player == null) return;
        if (!ev.IsAllowed) return;
        if (ev.Reason != SpawnReason.RoundStart) return;
        if (!IsAssignableFirstRoleTarget(ev.Player)) return;

        RoundStartCandidates.Add(ev.Player.Id);
        ev.IsAllowed = false;
        Log.Debug($"[FirstRoles] RoundStart candidate {DescribeRoleTarget(ev.Player)} vanilla={ev.NewRole} flags={ev.SpawnFlags} cancelled=true");
    }

    private static void OnPlayerSpawned(SpawnedEventArgs? ev)
    {
        if (ev?.Player == null) return;
        if (!_planBuilt) return;
        if (!PlannedRoles.ContainsKey(ev.Player.Id)) return;

        int playerId = ev.Player.Id;
        var reason = ev.Reason;
        Timing.CallDelayed(RoleSpawnTimings.NextFrame, () => ApplyPlannedRole(playerId, $"spawned:{reason}"));
    }

    private static void OnPlayerLeft(LeftEventArgs? ev)
    {
        if (ev?.Player == null) return;

        int playerId = ev.Player.Id;
        RoundStartCandidates.Remove(playerId);
        AppliedInitialRoles.Remove(playerId);
        PendingNpcRoleApplies.Remove(playerId);

        if (PlannedRoles.Remove(playerId))
            UnlockRoundIfPlanComplete("player-left");
    }

    private static bool TryChooseAssignableRole(List<WeightedRoleEntry> table, out object? choice)
    {
        choice = null;

        const int maxTries = 20;

        for (int i = 0; i < maxTries; i++)
        {
            choice = WeightedRole.Choose(table);
            if (choice != null && RoleLimitManager.CanAssign(choice))
                return true;
        }

        return false;
    }

    private static void PlanRole(Player? player, List<WeightedRoleEntry> table, RoleSpawnFlags flags)
    {
        if (!IsAssignableFirstRoleTarget(player)) return;
        if (!TryChooseAssignableRole(table, out var choice) || choice == null) return;

        RoleLimitManager.Consume(choice);
        PlannedRoles[player!.Id] = new PlannedInitialRole(choice, flags);
        Log.Debug($"[FirstRoles] Plan {DescribeRoleTarget(player)} -> {DescribePlannedRole(choice)}");
    }

    private static void ApplyPlannedRole(int playerId, string source, int attempt = 0)
    {
        if (!PlannedRoles.TryGetValue(playerId, out var planned)) return;
        if (AppliedInitialRoles.Contains(playerId)) return;

        var player = Player.Get(playerId);
        if (!IsAssignableFirstRoleTarget(player))
        {
            AppliedInitialRoles.Add(playerId);
            PlannedRoles.Remove(playerId);
            Log.Debug($"[FirstRoles] Drop planned role for unavailable player Id={playerId} ({source})");
            UnlockRoundIfPlanComplete(source);
            return;
        }

        if (player!.IsNPC && !IsNpcReadyForFirstRole(player))
        {
            ScheduleNpcRoleApplyRetry(playerId, source, attempt);
            return;
        }

        AppliedInitialRoles.Add(playerId);
        PlannedRoles.Remove(playerId);

        switch (planned.Role)
        {
            case RoleTypeId r:
                Log.Debug($"[FirstRoles] Apply {DescribeRoleTarget(player)} -> {r} ({source})");
                player.SetRole(r, planned.SpawnFlags);
                break;
            case CRoleTypeId cr:
                Log.Debug($"[FirstRoles] Apply {DescribeRoleTarget(player)} -> {cr} ({source})");
                player.SetRole(cr, planned.SpawnFlags);
                break;
        }

        PendingNpcRoleApplies.Remove(playerId);
        UnlockRoundIfPlanComplete(source);
    }

    private static void ScheduleNpcRoleApplyRetry(int playerId, string source, int attempt)
    {
        if (attempt >= MaxNpcApplyAttempts)
        {
            PendingNpcRoleApplies.Remove(playerId);
            AppliedInitialRoles.Add(playerId);
            PlannedRoles.Remove(playerId);
            Log.Warn($"[FirstRoles] Drop planned role for unstable NPC Id={playerId} after {attempt} attempts ({source})");
            UnlockRoundIfPlanComplete(source);
            return;
        }

        if (!PendingNpcRoleApplies.Add(playerId))
            return;

        Log.Debug($"[FirstRoles] Delay NPC initial role Id={playerId} attempt={attempt + 1}/{MaxNpcApplyAttempts} ({source})");
        Timing.CallDelayed(RoleSpawnTimings.FirstRolesNpcApplyRetry, () =>
        {
            PendingNpcRoleApplies.Remove(playerId);
            ApplyPlannedRole(playerId, $"npc-delay:{source}", attempt + 1);
        });
    }

    private static void _LimitChecker()
    {
        Log.Debug("[FirstRoles] _LimitChecker called");

        // 現在のモードに応じたロール上限を一括適用
        RoleLimitManager.ApplyPool(RoleTables.GetCurrentLimitPool());
    }

    private static void SetupRandomRoles()
    {
        PlannedRoles.Clear();
        AppliedInitialRoles.Clear();
        _planBuilt = false;

        if (_roundUnlockFallback.IsRunning)
            Timing.KillCoroutines(_roundUnlockFallback);

        _LimitChecker();
        OnAssign();

        var players = GetRoundStartTargets();

        if (players.Count == 0)
        {
            UnlockRound("no eligible players");
            return;
        }

        var shuffledPlayers = players.OrderBy(_ => Random.value).ToList();
        int scpCount = Mathf.Min(shuffledPlayers.Count, Mathf.Max(1, shuffledPlayers.Count / 5));

        var scpPlayers = shuffledPlayers.Take(scpCount).ToList();
        var humanPlayers = shuffledPlayers.Skip(scpCount).ToList();

        foreach (var pl in scpPlayers)
        {
            if (IsAssignableFirstRoleTarget(pl))
                PlanRole(pl, RoleTables.GetScpRoles(), RoleSpawnFlags.All);
        }

        for (int i = 0; i < humanPlayers.Count; i++)
        {
            var pl = humanPlayers[i];
            if (!IsAssignableFirstRoleTarget(pl))
                continue;

            var table = (i % 3) switch
            {
                0 => RoleTables.GetClassDRoles(),
                1 => RoleTables.GetScientistRoles(),
                _ => RoleTables.GetGuardRoles(),
            };

            PlanRole(pl, table, RoleSpawnFlags.All);
        }

        _planBuilt = true;

        if (PlannedRoles.Count == 0)
        {
            UnlockRound("no planned roles");
            return;
        }

        Log.Debug($"[FirstRoles] Planned {PlannedRoles.Count}/{players.Count} initial roles");
        ScheduleRoundUnlockFallback();
        Timing.CallDelayed(RoleSpawnTimings.NextFrame, () => ApplyPlanToReadyPlayers("round-start-sweep"));
    }

    private static List<Player> GetRoundStartTargets()
    {
        var candidates = RoundStartCandidates
            .Select(Player.Get)
            .Where(IsAssignableFirstRoleTarget)
            .ToList();

        if (candidates.Count > 0)
            return candidates;

        Log.Warn("[FirstRoles] No RoundStart ChangingRole candidates were recorded; falling back to alive/unassigned players.");
        return Player.List
            .Where(player => IsAssignableFirstRoleTarget(player) && (player!.IsAlive || player.IsRoleUnassigned()))
            .ToList();
    }

    private static void ApplyPlanToReadyPlayers(string source)
    {
        foreach (int playerId in PlannedRoles.Keys.ToList())
            ApplyPlannedRole(playerId, source);

        UnlockRoundIfPlanComplete(source);
    }

    private static void ScheduleRoundUnlockFallback()
    {
        _roundUnlockFallback = Timing.CallDelayed(RoleSpawnTimings.FirstRolesRoundUnlockFallback, () =>
        {
            ApplyPlanToReadyPlayers("unlock-fallback");

            if (PlannedRoles.Count > 0)
            {
                Log.Warn($"[FirstRoles] Unlock fallback dropped {PlannedRoles.Count} pending initial roles.");
                PlannedRoles.Clear();
            }

            UnlockRound("fallback");
        });
    }

    private static void UnlockRoundIfPlanComplete(string reason)
    {
        if (PlannedRoles.Count == 0)
            UnlockRound($"plan complete ({reason})");
    }

    private static void UnlockRound(string reason)
    {
        if (_roundUnlockFallback.IsRunning)
            Timing.KillCoroutines(_roundUnlockFallback);

        Round.IsLocked = false;
        Log.Debug($"[FirstRoles] Round unlocked: {reason}");
    }

    private static bool IsAssignableFirstRoleTarget(Player? player)
    {
        return player != null
               && player.ReferenceHub != null
               && !player.IsHost
               && !player.ReferenceHub.IsHost
               && !CRole.IsTeamNpc(player);
    }

    private static bool IsNpcReadyForFirstRole(Player player)
    {
        if (!player.IsNPC)
            return true;

        return player.ReferenceHub != null
               && player.ReferenceHub.netId != 0
               && player.ReferenceHub.authManager != null;
    }

    private static string DescribePlannedRole(object role)
    {
        return role switch
        {
            RoleTypeId r => r.ToString(),
            CRoleTypeId cr => cr.ToString(),
            _ => role.ToString(),
        };
    }

    private static string DescribeRoleTarget(Player player)
    {
        return $"{player.Nickname} (Id={player.Id}, NetId={player.ReferenceHub.netId}, IsNPC={player.IsNPC}, IsHost={player.IsHost}, HubIsHost={player.ReferenceHub.IsHost}, TeamNpc={CRole.IsTeamNpc(player)})";
    }

    private static void OnAssign()
    {
        if (MapFlags.GetSeason() is SeasonTypeId.April)
        {
            RoleTables.SetCurrentMode("April");
        }
        else
        {
            RoleTables.SetCurrentMode("Normal");
        }
    }
}
