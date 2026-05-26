using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using HintServiceMeow.Core.Extension;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Features.RoundVictory.Core;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.Hints;

namespace Slafight_Plugin_EXILED.CustomRoles;

public class CustomRolesHandler : IBootstrapHandler, IDisposable
{
    public static CustomRolesHandler Instance { get; private set; }
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
    private CoroutineHandle _conditionCoroutine;
    private CoroutineHandle _roundLockCoroutine;

    public CustomRolesHandler()
    {
        Exiled.Events.Handlers.Player.Hurting += OnHurting;
        Exiled.Events.Handlers.Player.ChangingRole += CustomRoleRemover;
        Exiled.Events.Handlers.Server.RoundStarted += RoundCoroutine;
        Exiled.Events.Handlers.Server.EndingRound += CancelEnd;
        Exiled.Events.Handlers.Server.WaitingForPlayers += ResetAbilities;
        Exiled.Events.Handlers.Server.RestartingRound += AbilityResetInRoundRestarting;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Exiled.Events.Handlers.Player.Hurting -= OnHurting;
        Exiled.Events.Handlers.Player.ChangingRole -= CustomRoleRemover;
        Exiled.Events.Handlers.Server.RoundStarted -= RoundCoroutine;
        Exiled.Events.Handlers.Server.EndingRound -= CancelEnd;
        Exiled.Events.Handlers.Server.WaitingForPlayers -= ResetAbilities;
        Exiled.Events.Handlers.Server.RestartingRound -= AbilityResetInRoundRestarting;

        if (_conditionCoroutine.IsRunning)
            Timing.KillCoroutines(_conditionCoroutine);

        if (_roundLockCoroutine.IsRunning)
            Timing.KillCoroutines(_roundLockCoroutine);

        GC.SuppressFinalize(this);
    }

    public void ResetAbilities()
    {
        AbilityResetUtil.ResetAllAbilities();
    }

    public void RoundCoroutine()
    {
        if (_conditionCoroutine.IsRunning)
            Timing.KillCoroutines(_conditionCoroutine);

        _conditionCoroutine = Timing.RunCoroutine(DelayUnlessLobby(
            10f,
            () => _conditionCoroutine = Timing.RunCoroutine(UniversalConditionCoroutine())));
    }

    private IEnumerator<float> UniversalConditionCoroutine()
    {
        for (;;)
        {
            if (Round.IsLobby)
                yield break;

            var result = RoundVictoryEvaluator.Evaluate(GetAliveRoundPlayers());

            if (result.Triggered)
            {
                Log.Debug($"[RoundCondition] {result.DebugName} triggered");

                if (TryExecuteCondition(result) && result.IsForEnd)
                    yield break;
            }

            yield return Timing.WaitForSeconds(1f);
        }
    }

    private static bool TryExecuteCondition(RoundVictoryResult result)
    {
        try
        {
            if (result.IsForEnd)
                Round.IsLocked = false;

            result.Execute?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"[RoundCondition] {result.DebugName} execution failed: {ex}");
            return false;
        }
    }

    private static List<Player> GetAliveRoundPlayers()
    {
        return Player.List
            .Where(IsAliveRoundPlayer)
            .ToList();
    }

    private static bool IsAliveRoundPlayer(Player? player)
    {
        return player != null && player.IsAlive && player.Role.Type != RoleTypeId.Spectator;
    }

    private void CancelEnd(EndingRoundEventArgs ev)
    {
        if (!ShouldLockRoundEnd())
            return;

        ev.IsAllowed = false;
        Round.IsLocked = true;

        if (!_roundLockCoroutine.IsRunning)
            _roundLockCoroutine = Timing.RunCoroutine(RoundLocker());
    }

    private IEnumerator<float> RoundLocker()
    {
        for (;;)
        {
            if (Round.IsLobby || !ShouldLockRoundEnd())
            {
                Round.IsLocked = false;
                yield break;
            }

            yield return Timing.WaitForSeconds(1f);
        }
    }

    private static bool HasSpecificWinPlayers()
    {
        return Player.List.Any(player => player != null && player.HasSpecificWinMethod());
    }

    private static bool ShouldLockRoundEnd()
    {
        return HasSpecificWinPlayers() ||
               EvacuationRoundEndState.ShouldDeferRoundEnd(GetAliveRoundPlayers());
    }

    private static void OnHurting(HurtingEventArgs ev)
    {
        if (ev.Player == null)
            return;

        if (ev.Attacker?.GetCustomRole() == CRoleTypeId.Scp3005 ||
            ev.Attacker?.GetCustomRole() == CRoleTypeId.FifthistPriest)
        {
            if (ev.Player.HasFlag(SpecificFlagType.AntiMemeEffectDisabled))
                ev.IsAllowed = false;
        }
    }
    
    private static void CustomRoleRemover(ChangingRoleEventArgs ev)
    {
        if (!ev.IsAllowed) return;
        Log.Debug($"[CustomRoleRemover] Reset ALL for {ev.Player?.Nickname} (role change {ev.Player?.Role} -> {ev.NewRole})");

        ev.Player!.UniqueRole = null;
        ev.Player.CustomInfo = null;
        ev.Player.Scale = new Vector3(1f, 1f, 1f);
        ev.Player.IsSpectatable = true;
        ev.Player.IsGodModeEnabled = false;
        ev.Player.IsNoclipPermitted = false;
        ev.Player.IsBypassModeEnabled = false;
        ev.Player.ClearCustomInfo();
        ev.Player.DisableAllEffects();

        var player = ev.Player;
        player.Clear();
        AbilityManager.ClearSlots(player);
        AbilityBase.RevokeAbility(player.Id);
        CustomShieldState.Clear(player);
        
        var display = player.GetPlayerDisplay();
        display.TryGetHint("CRoleSpawnedHint", out var oldHint);
        if (oldHint != null) player.RemoveHint(oldHint);
        
        CItem.RebuildHybridStateFor(player);

        Timing.CallDelayed(1f, () =>
        {
            try
            {
                if (player.IsConnected)
                    PlayerHUD.Instance.HintSync(SyncType.PHUD_Specific, string.Empty, player);

                RoleSpecificTextProvider.Clear(player);
            }
            catch
            {
                // ignore
            }
        });
    }

    public static void AbilityResetInRoundRestarting()
    {
        AbilityManager.Loadouts.Clear();
        AbilityBase.RevokeAllPlayers();
    }

    public static void EndRound(CTeam winnerTeam = CTeam.SCPs, string specificReason = null, bool tryUseTeamDefine = false)
    {
        RoundEndExecutor.EndRound(winnerTeam, specificReason, tryUseTeamDefine);
    }

    public static void EndRound(CTeamGroup winnerGroup, string specificReason = null)
    {
        RoundEndExecutor.EndRound(winnerGroup, specificReason);
    }

    private static IEnumerator<float> DelayUnlessLobby(float delay, Action action)
    {
        var remaining = delay;
        while (remaining > 0f)
        {
            if (Round.IsLobby)
                yield break;

            var wait = Math.Min(0.5f, remaining);
            remaining -= wait;
            yield return Timing.WaitForSeconds(wait);
        }

        if (Round.IsLobby)
            yield break;

        action();
    }
}
