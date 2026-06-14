using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Objects;
using Slafight_Plugin_EXILED.CustomMaps;
using UnityEngine;
using MEC;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.MainHandlers;

public class FirstRolesHandler : IBootstrapHandler
{
    public static FirstRolesHandler Instance { get; private set; }
    public static void Register() => Instance = new();
    public static void Unregister() => Instance = null;

    // FirstRoles で割り当て済みかどうかを Player ごとに管理
    private static readonly Dictionary<Player, bool> FirstRoleAssigned = new();

    // RoundStart 由来の Spawned プレイヤーを収集
    private List<Player> _roundStartSpawnQueue;
    private int _assignedCount;
    private bool _assigning;
    private CoroutineHandle _coroutineHandle;

    public FirstRolesHandler()
    {
        Exiled.Events.Handlers.Server.WaitingForPlayers += OnWaitingForPlayers;
        Exiled.Events.Handlers.Server.RoundStarted      += OnRoundStarted;
        Exiled.Events.Handlers.Player.Spawned           += OnPlayerSpawned;
    }

    ~FirstRolesHandler()
    {
        Exiled.Events.Handlers.Server.WaitingForPlayers -= OnWaitingForPlayers;
        Exiled.Events.Handlers.Server.RoundStarted      -= OnRoundStarted;
        Exiled.Events.Handlers.Player.Spawned           -= OnPlayerSpawned;
    }

    private void OnWaitingForPlayers()
    {
        Log.Debug("[FirstRoles] WaitingForPlayers: cleaning up state.");

        Timing.KillCoroutines(_coroutineHandle);

        _roundStartSpawnQueue = [];
        _assignedCount = 0;
        _assigning = false;
        _coroutineHandle = default;

        FirstRoleAssigned.Clear();

        Log.Debug("[FirstRoles] WaitingForPlayers: state cleaned up.");
    }

    private void OnRoundStarted()
    {
        _roundStartSpawnQueue = [];
        _assignedCount = 0;
        _assigning = false;

        _LimitChecker();
        OnAssign();

        Log.Debug("[FirstRoles] RoundStarted: collecting RoundStart spawns...");

        Round.IsLocked = true;

        _coroutineHandle = Timing.RunCoroutine(RoundStartAssignCoroutine());
    }

    private IEnumerator<float> RoundStartAssignCoroutine()
    {
        Log.Debug("[FirstRoles] RoundStartAssignCoroutine: started");

        // Queue が空なら Player.List から未割り当てプレイヤーを補足
        if (_roundStartSpawnQueue.Count == 0)
        {
            Log.Debug("[FirstRoles] Queue empty, trying to collect from Player.List...");
            var players = Player.List
                .Where(p => p != null && !p.IsHost && IsFirstRoleUnassigned(p))
                .ToList();

            Log.Debug($"[FirstRoles] Found {players.Count} unassigned players from Player.List");

            foreach (var p in players)
            {
                if (!_roundStartSpawnQueue.Contains(p))
                    _roundStartSpawnQueue.Add(p);
            }
        }

        if (_roundStartSpawnQueue.Count == 0)
        {
            Log.Debug("[FirstRoles] No players to assign roles.");
            Round.IsLocked = false;
            yield break;
        }

        _assigning = true;

        var shuffled = _roundStartSpawnQueue.OrderBy(_ => Random.value).ToList();
        int total    = shuffled.Count;
        int scpCount = Mathf.Max(1, total / 5);

        Log.Debug($"[FirstRoles] Assigning roles: total={total}, SCP={scpCount}");

        float maxDuration = 5.0f;
        float interval = total > 1 ? maxDuration / (total - 1) : 0f;

        for (int i = 0; i < shuffled.Count; i++)
        {
            var target = shuffled[i];
            if (target == null)
            {
                Log.Debug($"[FirstRoles] Skip null target at index {i}");
                continue;
            }

            if (!IsFirstRoleUnassigned(target))
            {
                Log.Debug($"[FirstRoles] Already assigned, skip: {target.Nickname}");
                continue;
            }

            bool isScp = i < scpCount;
            var table  = isScp
                ? RoleTables.GetScpRoles()
                : GetHumanRoleTable(_assignedCount);

            AssignRole(target, table, RoleSpawnFlags.All);
            _assignedCount++;

            Log.Debug($"[FirstRoles] Assigned role #{_assignedCount}/{total}: {target.Nickname} (isScp={isScp})");

            if (i < shuffled.Count - 1)
            {
                yield return interval;
            }
        }

        Log.Debug("[FirstRoles] All roles assigned (coroutine end).");
        Round.IsLocked = false;
    }

    private void OnPlayerSpawned(Exiled.Events.EventArgs.Player.SpawnedEventArgs ev)
    {
        var player = ev.Player;
        if (player == null || ev.Player.IsHost)
            return;

        // RoundStart 由来のみ収集
        if (ev.Reason != SpawnReason.RoundStart)
        {
            Log.Debug($"[FirstRoles] Spawned skipped: Reason != RoundStart ({ev.Reason})");
            return;
        }

        // 既に割り当て済みなら無視
        if (!IsFirstRoleUnassigned(player))
        {
            Log.Debug($"[FirstRoles] Spawned skipped: already assigned ({player.Nickname})");
            return;
        }

        // 重複收集防止
        if (_roundStartSpawnQueue.Contains(player))
        {
            Log.Debug($"[FirstRoles] Spawned skipped: already in queue ({player.Nickname})");
            return;
        }

        if (player.IsNPC)
        {
            Timing.CallDelayed(RoleSpawnTimings.FirstRolesNpcApplyRetry, () =>
            {
                if (player == null) return;
                _roundStartSpawnQueue.Add(player);
                Log.Debug(
                    $"[FirstRoles] Spawned(RoundStart) collected: {player.Nickname} (queue size={_roundStartSpawnQueue.Count})");
            });
        }
        else
        {
            _roundStartSpawnQueue.Add(player);
            Log.Debug($"[FirstRoles] Spawned(RoundStart) collected: {player.Nickname} (queue size={_roundStartSpawnQueue.Count})");
        }
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

    private static void AssignRole(Player player, List<WeightedRoleEntry> table, RoleSpawnFlags flags)
    {
        const int maxTries = 20;
        object? choice = null;

        for (int i = 0; i < maxTries; i++)
        {
            choice = WeightedRole.Choose(table);
            if (choice == null)
                continue;

            if (RoleLimitManager.CanAssign(choice))
                break;
        }

        if (choice == null)
        {
            Log.Debug($"[FirstRoles] No available role for {player.Nickname} (all limited).");
            return;
        }

        RoleLimitManager.Consume(choice);

        switch (choice)
        {
            case RoleTypeId r:
                player.SetRole(r, flags);
                break;
            case CRoleTypeId cr:
                player.SetRole(cr, flags);
                break;
        }

        // FirstRoles で割り当てたことを記録
        SetFirstRoleAssigned(player, true);

        Log.Debug($"[FirstRoles] Assigned {choice} to {player.Nickname}.");
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

    // FirstRoles 未割り当て判定
    private static bool IsFirstRoleUnassigned(Player player) =>
        player == null || !FirstRoleAssigned.GetValueOrDefault(player);

    // FirstRoles 割当て済みフラグセット
    private static void SetFirstRoleAssigned(Player player, bool value)
    {
        if (player == null)
            return;

        if (FirstRoleAssigned.ContainsKey(player))
            FirstRoleAssigned[player] = value;
        else
            FirstRoleAssigned.Add(player, value);
    }
}