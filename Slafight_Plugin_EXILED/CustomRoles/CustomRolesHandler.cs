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

public class CustomRolesHandler : IBootstrapHandler
{
    public static CustomRolesHandler Instance { get; private set; }
    public static void Register() { Instance = new(); }
    public static void Unregister() { Instance = null; }

    private readonly List<WinCondition> _winConditions;

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

    ~CustomRolesHandler()
    {
        Exiled.Events.Handlers.Player.Hurting -= OnHurting;
        Exiled.Events.Handlers.Player.ChangingRole -= CustomRoleRemover;
        Exiled.Events.Handlers.Server.RoundStarted -= RoundCoroutine;
        Exiled.Events.Handlers.Server.EndingRound -= CancelEnd;
        Exiled.Events.Handlers.Server.WaitingForPlayers -= ResetAbilities;
        Exiled.Events.Handlers.Server.RestartingRound -= AbilityResetInRoundRestarting;
    }

    public void ResetAbilities()
    {
        AbilityResetUtil.ResetAllAbilities();
    }

    public void RoundCoroutine()
    {
        Timing.CallDelayed(10f, () =>
        {
            Timing.RunCoroutine(UniversalConditionCoroutine());
        });
    }

    private IEnumerator<float> UniversalConditionCoroutine()
    {
        for (;;)
        {
            if (Round.IsLobby)
                yield break;

            var alivePlayers = Player.List
                .Where(p => p != null && p.IsAlive && p.Role.Type != RoleTypeId.Spectator)
                .ToList();

            foreach (var condition in _winConditions.Where(c => c.IsEnabled))
            {
                if (condition.Check(alivePlayers))
                {
                    Log.Debug($"[RoundCondition] {condition.DebugName} triggered");
                    if (condition.IsForEnd)
                    {
                        Round.IsLocked = false;
                        condition.ExecuteAction();
                        yield break;
                    }

                    condition.ExecuteAction();
                }
            }

            yield return Timing.WaitForSeconds(1f);
        }
    }

    private static bool CheckAIKill(List<Player> players)
    {
        int scpCount = 0;
        bool only079 = true;
        List<Player> scp079s = [];

        foreach (var player in players)
        {
            if (player.GetTeam() != CTeam.SCPs)
                continue;

            scpCount++;

            if (player.Role.Type == RoleTypeId.Scp079)
                scp079s.Add(player);
            else
                only079 = false;
        }

        bool scpSideIsOnly079 = scpCount > 0 && only079 && scp079s.Count == scpCount;

        var nonScpTeams = players
            .Where(p => p.GetTeam() != CTeam.SCPs)
            .GroupBy(p => p.GetTeam())
            .Count();

        return scpSideIsOnly079 && nonScpTeams == 1;
    }

    private static void ExecuteAIKill()
    {
        var scp079s = Player.List
            .Where(p => p != null && p.IsAlive && p.Role.Type == RoleTypeId.Scp079)
            .ToList();

        foreach (var p in scp079s)
        {
            p.Kill(
                "Terminated by C.A.S.S.I.E."
            );
        }
        
        Exiled.API.Features.Cassie.MessageTranslated("SCP-079 has been terminated by Central Autonomic Service System for Internal Emergencies.", "<color=red>SCP-079</color>は<color=yellow>C.A.S.S.I.E</color>により終了されました。");
        NewEventHandler.RecoverControl(FacilityControlRecoverType.DisableTesla);
    }
    
    private static bool CheckAraorunKill(List<Player> players)
    {
        int scpCount = 0;
        bool only079 = true;
        List<Player> scp079s = [];

        foreach (var player in players)
        {
            if (player.GetTeam().IsGoI())
                continue;

            scpCount++;

            if (player.Role.Type == RoleTypeId.Scp079)
                scp079s.Add(player);
            else
                only079 = false;
        }

        bool scpSideIsOnly079 = scpCount > 0 && only079 && scp079s.Count == scpCount;

        var nonScpTeams = players
            .Where(p => p.GetTeam().IsGoI())
            .GroupBy(p => p.GetTeam())
            .Count();

        return scpSideIsOnly079 && nonScpTeams == 1;
    }

    private static void ExecuteAraOrunKill()
    {
        var scp079s = Player.List
            .Where(p => p != null && p.IsAlive && p.Role.Type == RoleTypeId.Scp079)
            .ToList();

        foreach (var p in scp079s)
        {
            p.Kill(
                "Terminated by Fifthists"
            );
        }
        
        Exiled.API.Features.Cassie.MessageTranslated("Unknown Subject has been terminated by SCP 3 1 2 5", $"<color=yellow>アラ・オルン</color>は<color={CTeam.Fifthists.GetTeamColor()}>SCP-3125</color>により終了されました。");
    }

    private static void CancelEnd(EndingRoundEventArgs ev)
    {
        int count = 0;

        foreach (var player in Player.List)
        {
            if (player != null && player.HasSpecificWinMethod())
                count++;
        }

        if (count == 0)
            return;

        ev.IsAllowed = false;
        Round.IsLocked = true;
        Timing.RunCoroutine(RoundLocker());
    }

    private static IEnumerator<float> RoundLocker()
    {
        for (;;)
        {
            var count = Player.List.OfType<Player>().Count(player => player.HasSpecificWinMethod());

            if (count == 0)
            {
                Round.IsLocked = false;
                yield break;
            }

            yield return Timing.WaitForSeconds(1f);
        }
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

                Timing.CallDelayed(10f, () =>
                {
                    if (!Round.IsLobby)
                        StaticUtils.TryRestart();
                });
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

                    Timing.CallDelayed(10f, () =>
                    {
                        if (!Round.IsLobby)
                            StaticUtils.TryRestart();
                    });
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

                        Timing.CallDelayed(10f, () =>
                        {
                            if (!Round.IsLobby)
                                StaticUtils.TryRestart();
                        });
                        break;

                    case "CANDY_WIN":
                        foreach (var player in Player.List)
                        {
                            player.ShowHint("<b><size=80><color=#ff96de>お菓子の戦士達</color>の勝利</size></b>", 555f);
                            Intercom.TrySetOverride(player, true);
                        }

                        Timing.CallDelayed(10f, () =>
                        {
                            if (!Round.IsLobby)
                                StaticUtils.TryRestart();
                        });
                        break;

                    default:
                        foreach (var player in Player.List)
                        {
                            player.ShowHint("<b><size=80><color=#ffffff>UNKNOWN TEAM</color>の勝利</size></b>", 555f);
                            Intercom.TrySetOverride(player, true);
                        }

                        Timing.CallDelayed(10f, () =>
                        {
                            if (!Round.IsLobby)
                                StaticUtils.TryRestart();
                        });
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

                    Timing.CallDelayed(10f, () =>
                    {
                        if (!Round.IsLobby)
                            StaticUtils.TryRestart();
                    });
                }
                else
                {
                    foreach (var player in Player.List)
                    {
                        player.ShowHint("<b><size=80><color=#0000c8>世界オカルト連合</color>の勝利</size></b>", 555f);
                        Intercom.TrySetOverride(player, true);
                    }

                    Timing.CallDelayed(10f, () =>
                    {
                        if (!Round.IsLobby)
                            StaticUtils.TryRestart();
                    });
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

                Timing.CallDelayed(10f, () =>
                {
                    if (!Round.IsLobby)
                        StaticUtils.TryRestart();
                });
                break;
        }
    }
}
