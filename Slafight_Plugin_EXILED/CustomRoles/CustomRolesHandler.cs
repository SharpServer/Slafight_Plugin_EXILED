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
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.Hints;
using Slafight_Plugin_EXILED.MainHandlers;
using Slafight_Plugin_EXILED.SpecialEvents;

namespace Slafight_Plugin_EXILED.CustomRoles;

public sealed class WinCondition(
    CTeam team,
    string debugName,
    Func<List<Player>, bool> checkFunc,
    Action executeAction,
    Func<bool>? enableCondition = null,
    bool isForEnd = true)
{
    public CTeam Team { get; } = team;
    public string DebugName { get; } = debugName;
    private Func<List<Player>, bool> CheckFunc { get; } = checkFunc;
    public Action ExecuteAction { get; } = executeAction;
    public Func<bool>? EnableCondition { get; } = enableCondition;
    public bool IsForEnd { get; } = isForEnd;

    public bool IsEnabled => EnableCondition?.Invoke() ?? true;

    public bool Check(List<Player> players) => CheckFunc(players);
}

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

    private readonly List<WinCondition> _winConditions;
    private bool _disposed;
    private CoroutineHandle _conditionCoroutine;
    private CoroutineHandle _roundLockCoroutine;

    public CustomRolesHandler()
    {
        _winConditions =
        [
            new(
                CTeam.Fifthists,
                "FifthistWin",
                players => players.IsOnlyTeam(CTeam.Fifthists),
                () => EndRound(CTeam.Fifthists)
            ),
            new(
                CTeam.Others,
                "SnowWarrierWin",
                players => players.IsOnlyTeam(CTeam.Others, "snow"),
                () => EndRound(CTeam.Others, "SW_WIN"),
                () => MapFlags.GetSeason() == SeasonTypeId.Christmas
            ),
            new(
                CTeam.Others,
                "CandyWarrierWin",
                players => players.IsOnlyTeam(CTeam.Others, "candy"),
                () => EndRound(CTeam.Others, "CANDY_WIN"),
                () => MapFlags.GetSeason() is SeasonTypeId.April or SeasonTypeId.Halloween
            ),
            new(
                CTeam.SCPs,
                "AIWin",
                CheckAIKill,
                ExecuteAIKill,
                isForEnd: false
            ),
            new(
                CTeam.FoundationForces,
                "AraOrunDeath",
                CheckAraorunKill,
                ExecuteAraOrunKill,
                () => SpecialEventsHandler.Instance.NowEvent is SpecialEventType.CaseColourlessGreen,
                isForEnd: false
            )
        ];

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

            var alivePlayers = GetAliveRoundPlayers();

            foreach (var condition in _winConditions)
            {
                if (!TryCheckCondition(condition, alivePlayers))
                    continue;

                Log.Debug($"[RoundCondition] {condition.DebugName} triggered");

                if (!TryExecuteCondition(condition))
                    continue;

                if (condition.IsForEnd)
                {
                    yield break;
                }

                break;
            }

            yield return Timing.WaitForSeconds(1f);
        }
    }

    private static bool TryCheckCondition(WinCondition condition, List<Player> alivePlayers)
    {
        try
        {
            return condition.IsEnabled && condition.Check(alivePlayers);
        }
        catch (Exception ex)
        {
            Log.Error($"[RoundCondition] {condition.DebugName} check failed: {ex}");
            return false;
        }
    }

    private static bool TryExecuteCondition(WinCondition condition)
    {
        try
        {
            if (condition.IsForEnd)
                Round.IsLocked = false;

            condition.ExecuteAction();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"[RoundCondition] {condition.DebugName} execution failed: {ex}");
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

    private static bool CheckAIKill(List<Player> players)
    {
        return HasOnlyTargetRoleOnSideAgainstSingleOpposingTeam(
            players,
            player => player.GetTeam() == CTeam.SCPs,
            IsScp079Player);
    }

    private static void ExecuteAIKill()
    {
        var terminated = TerminatePlayers(IsScp079Player, "Terminated by C.A.S.S.I.E.");

        if (terminated == 0)
            return;
        
        Exiled.API.Features.Cassie.MessageTranslated("SCP-079 has been terminated by Central Autonomic Service System for Internal Emergencies.", "<color=red>SCP-079</color>は<color=yellow>C.A.S.S.I.E</color>により終了されました。");
        NewEventHandler.RecoverControl(FacilityControlRecoverType.DisableTesla);
    }
    
    private static bool CheckAraorunKill(List<Player> players)
    {
        return HasOnlyTargetRoleOnSideAgainstSingleOpposingTeam(
            players,
            player => !player.GetTeam().IsGoI(),
            IsAraOrunPlayer);
    }

    private static void ExecuteAraOrunKill()
    {
        var terminated = TerminatePlayers(IsAraOrunPlayer, "Terminated by Fifthists");

        if (terminated == 0)
            return;

        Exiled.API.Features.Cassie.MessageTranslated("Unknown Subject has been terminated by SCP 3 1 2 5", $"<color=yellow>アラ・オルン</color>は<color={CTeam.Fifthists.GetTeamColor()}>SCP-3125</color>により終了されました。");
    }

    private static bool HasOnlyTargetRoleOnSideAgainstSingleOpposingTeam(
        List<Player> players,
        Func<Player, bool> isSideMember,
        Func<Player, bool> isTargetRole)
    {
        int sidePlayerCount = 0;
        int targetRoleCount = 0;
        HashSet<CTeam> opposingTeams = [];

        foreach (var player in players)
        {
            if (!IsAliveRoundPlayer(player))
                continue;

            if (isSideMember(player))
            {
                sidePlayerCount++;

                if (isTargetRole(player))
                {
                    targetRoleCount++;
                    continue;
                }

                return false;
            }

            opposingTeams.Add(player.GetTeam());
        }

        return sidePlayerCount > 0 &&
               targetRoleCount == sidePlayerCount &&
               opposingTeams.Count == 1;
    }

    private static bool IsScp079Player(Player player)
    {
        return IsAliveRoundPlayer(player) &&
               player.GetTeam() == CTeam.SCPs &&
               player.Role.Type == RoleTypeId.Scp079;
    }

    private static bool IsAraOrunPlayer(Player player)
    {
        return IsAliveRoundPlayer(player) &&
               player.GetCustomRole() == CRoleTypeId.AraOrun;
    }

    private static int TerminatePlayers(Func<Player, bool> selector, string reason)
    {
        var targets = Player.List
            .Where(player => player != null && selector(player))
            .ToList();

        foreach (var target in targets)
        {
            target.Kill(reason);
        }

        return targets.Count;
    }

    private void CancelEnd(EndingRoundEventArgs ev)
    {
        if (!HasSpecificWinPlayers())
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
            if (Round.IsLobby || !HasSpecificWinPlayers())
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

    private WinCondition? GetCondition(string debugName)
    {
        return _winConditions.FirstOrDefault(x => x.DebugName == debugName);
    }

    public void UpdateWinConditionStates()
    {
        foreach (var condition in _winConditions.Where(condition => condition.DebugName is not ("FifthistWin" or "AIWin")).Where(condition => condition.EnableCondition == null))
        {
            continue;
        }
    }

    public static void EndRound(CTeam winnerTeam = CTeam.SCPs, string specificReason = null)
    {
        foreach (var player in Player.List)
        {
            player?.ShowHint("");
        }

        switch (winnerTeam)
        {
            case CTeam.SCPs:
                Round.KillsByScp = 999;
                Round.EndRound(true);
                break;

            case CTeam.Fifthists:
                Round.KillsByScp = 555;
                foreach (Player player in Player.List)
                {
                    player.ShowHint("<b><size=80><color=#ff00fa>第五教会</color>の勝利</size></b>", 555f);
                    Intercom.TrySetOverride(player, true);
                }

                ScheduleRestartIfRoundActive();
                break;

            case CTeam.ChaosInsurgency:
            case CTeam.ClassD:
                Round.EscapedDClasses = 999;
                Round.EndRound(true);
                break;

            case CTeam.FoundationForces:
            case CTeam.Scientists:
            case CTeam.Guards:
                Round.EscapedScientists = 999;

                if (specificReason == "NoHumanityAllowed")
                {
                    foreach (var player in Player.List)
                    {
                        player.ShowHint("<b><size=80><color=red>正常性</color>の勝利</size></b>", 555f);
                        Intercom.TrySetOverride(player, true);
                    }

                    ScheduleRestartIfRoundActive();
                }
                else
                {
                    Round.EndRound(true);
                }
                break;

            case CTeam.Others:
                Round.EscapedDClasses = 999;

                switch (specificReason)
                {
                    case "SW_WIN":
                        foreach (var player in Player.List)
                        {
                            player.ShowHint("<b><size=80><color=#ffffff>雪の戦士達</color>の勝利</size></b>", 555f);
                            Intercom.TrySetOverride(player, true);
                        }

                        ScheduleRestartIfRoundActive();
                        break;

                    case "CANDY_WIN":
                        foreach (var player in Player.List)
                        {
                            player.ShowHint("<b><size=80><color=#ff96de>お菓子の戦士達</color>の勝利</size></b>", 555f);
                            Intercom.TrySetOverride(player, true);
                        }

                        ScheduleRestartIfRoundActive();
                        break;

                    default:
                        foreach (var player in Player.List)
                        {
                            player.ShowHint("<b><size=80><color=#ffffff>UNKNOWN TEAM</color>の勝利</size></b>", 555f);
                            Intercom.TrySetOverride(player, true);
                        }

                        ScheduleRestartIfRoundActive();
                        break;
                }
                break;

            case CTeam.GoC:
                Round.EscapedDClasses = 999;

                if (specificReason == "SavedHumanity")
                {
                    foreach (var player in Player.List)
                    {
                        player.ShowHint("<b><size=80><color=#0000c8>人類</color>の勝利</size></b>", 555f);
                        Intercom.TrySetOverride(player, true);
                    }

                    ScheduleRestartIfRoundActive();
                }
                else
                {
                    foreach (var player in Player.List)
                    {
                        player.ShowHint("<b><size=80><color=#0000c8>世界オカルト連合</color>の勝利</size></b>", 555f);
                        Intercom.TrySetOverride(player, true);
                    }

                    ScheduleRestartIfRoundActive();
                }
                break;

            case CTeam.Null:
            case CTeam.UIU:
            case CTeam.SerpentsHand:
            case CTeam.BrokenGodChurch:
            case CTeam.O5:
            case CTeam.Sarkic:
            case CTeam.AWCY:
            case CTeam.BlackQueen:
            default:
                foreach (var player in Player.List)
                {
                    player.ShowHint("<b><size=80><color=#ffffff>UNKNOWN TEAM</color>の勝利</size></b>", 555f);
                    Intercom.TrySetOverride(player, true);
                }

                ScheduleRestartIfRoundActive();
                break;
        }
    }

    private static void ScheduleRestartIfRoundActive()
    {
        Timing.RunCoroutine(DelayUnlessLobby(10f, StaticUtils.TryRestart));
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
