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

    private void CancelRoundStartedRole(ChangingRoleEventArgs ev)
    {
        if (ev.Reason == SpawnReason.RoundStart)
        {
            ev.IsAllowed = false;
        }
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
            Log.Debug($"[FirstRoles] No available role for {player.Nickname} (all limited)");
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

        Log.Debug($"[FirstRoles] Assigned {choice} to {player.Nickname}");
    }

    private static void _LimitChecker()
    {
        Log.Debug("[FirstRoles] _LimitChecker called");

        // 現在のモードに応じたロール上限を一括適用
        RoleLimitManager.ApplyPool(RoleTables.GetCurrentLimitPool());
    }

    private static void SetupRandomRoles()
    {
        Log.Debug("[FirstRoles] SetupRandomRoles called");

        _LimitChecker();

        var players = Player.List
            .Where(p => p != null && p.IsConnected && p.IsRoleUnassigned())
            .ToList();

        int playerCount = players.Count;
        if (playerCount == 0)
        {
            Round.IsLocked = false;
            return;
        }

        var shuffledPlayers = players.OrderBy(_ => Random.value).ToList();

        // 1) SCP 人数（5人に1人、最低1人）
        var scpCount = Mathf.Max(1, playerCount / 5);

        var scpPlayers   = shuffledPlayers.Take(scpCount).ToList();
        var humanPlayers = shuffledPlayers.Skip(scpCount).ToList();

        // OnAssign
        OnAssign();
        
        // SCP ロール
        foreach (var pl in scpPlayers)
            AssignRole(pl, RoleTables.GetScpRoles(), RoleSpawnFlags.All);

        // 人間：3人に1人
        for (var i = 0; i < humanPlayers.Count; i++)
        {
            var pl = humanPlayers[i];
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
