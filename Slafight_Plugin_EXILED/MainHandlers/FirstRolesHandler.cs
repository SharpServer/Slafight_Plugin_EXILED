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

    public FirstRolesHandler()
    {
        Exiled.Events.Handlers.Server.WaitingForPlayers += RoundLocker;
        Exiled.Events.Handlers.Player.ChangingRole += CancelRoundStartedRole;
        Exiled.Events.Handlers.Server.RoundStarted += SetupRandomRoles;
        Exiled.Events.Handlers.Server.RoundStarted += RoundUnlocker;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Exiled.Events.Handlers.Server.WaitingForPlayers -= RoundLocker;
        Exiled.Events.Handlers.Player.ChangingRole -= CancelRoundStartedRole;
        Exiled.Events.Handlers.Server.RoundStarted -= SetupRandomRoles;
        Exiled.Events.Handlers.Server.RoundStarted -= RoundUnlocker;
        System.GC.SuppressFinalize(this);
    }

    private static void RoundLocker()
    {
        Round.IsLocked = true;
    }

    private static void CancelRoundStartedRole(ChangingRoleEventArgs? ev)
    {
        if (ev == null) return;
        if (ev.Reason == SpawnReason.RoundStart)
            ev.IsAllowed = false;
    }

    private static void AssignRole(Player? player, List<WeightedRoleEntry> table, RoleSpawnFlags flags)
    {
        if (!IsEligibleInitialRoleTarget(player)) return;

        const int maxTries = 20;
        object? choice = null;

        for (int i = 0; i < maxTries; i++)
        {
            choice = WeightedRole.Choose(table);
            if (choice != null && RoleLimitManager.CanAssign(choice))
                break;
        }

        if (choice == null) return;
        if (!RoleLimitManager.CanAssign(choice)) return;
        if (!IsEligibleInitialRoleTarget(player)) return;

        RoleLimitManager.Consume(choice);

        switch (choice)
        {
            case RoleTypeId r:
                Log.Debug($"[FirstRoles] Assign {DescribeRoleTarget(player)} -> {r}");
                player.SetRole(r, flags);
                break;
            case CRoleTypeId cr:
                Log.Debug($"[FirstRoles] Assign {DescribeRoleTarget(player)} -> {cr}");
                player.SetRole(cr, flags);
                break;
        }
    }

    private static void _LimitChecker()
    {
        Log.Debug("[FirstRoles] _LimitChecker called");

        // 現在のモードに応じたロール上限を一括適用
        RoleLimitManager.ApplyPool(RoleTables.GetCurrentLimitPool());
    }

    private static void SetupRandomRoles()
    {
        _LimitChecker();

        var players = Player.List
            .Where(IsEligibleInitialRoleTarget)
            .ToList();

        if (players.Count == 0)
        {
            Round.IsLocked = false;
            return;
        }

        var shuffledPlayers = players.OrderBy(_ => Random.value).ToList();
        int scpCount = Mathf.Max(1, shuffledPlayers.Count / 5);

        var scpPlayers = shuffledPlayers.Take(scpCount).ToList();
        var humanPlayers = shuffledPlayers.Skip(scpCount).ToList();

        OnAssign();

        foreach (var pl in scpPlayers)
        {
            if (IsEligibleInitialRoleTarget(pl))
                AssignRole(pl, RoleTables.GetScpRoles(), RoleSpawnFlags.All);
        }

        for (int i = 0; i < humanPlayers.Count; i++)
        {
            var pl = humanPlayers[i];
            if (!IsEligibleInitialRoleTarget(pl))
                continue;

            var table = (i % 3) switch
            {
                0 => RoleTables.GetClassDRoles(),
                1 => RoleTables.GetScientistRoles(),
                _ => RoleTables.GetGuardRoles(),
            };

            AssignRole(pl, table, RoleSpawnFlags.All);
        }

        Round.IsLocked = false;
    }

    private static void RoundUnlocker()
    {
        Timing.CallDelayed(RoleSpawnTimings.FirstRolesRoundUnlockFallback, () =>
        {
            Round.IsLocked = false;
        });
    }

    private static bool IsEligibleInitialRoleTarget(Player? player)
    {
        return player != null
               && player.IsConnected
               && player.ReferenceHub != null
               && !player.IsHost
               && !player.ReferenceHub.IsHost
               && !CRole.IsTeamNpc(player)
               && player.IsRoleUnassigned();
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
