#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Utilities;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.MainHandlers;
using Slafight_Plugin_EXILED.SpecialEvents;
using UnityEngine;
using Hint = HintServiceMeow.Core.Models.Hints.Hint;

using Slafight_Plugin_EXILED.API.Interface;

namespace Slafight_Plugin_EXILED.Hints;

public class PlayerHUD : IBootstrapHandler, IDisposable
{
    public static PlayerHUD? Instance { get; private set; }
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

    private CoroutineHandle _specificAbilityLoop;
    private CoroutineHandle _abilityHudLoop;
    private CoroutineHandle _taskSyncLoop;
    private CoroutineHandle _debugHudLoop;

    // 観戦者ID → 現在見ているプレイヤー
    private readonly Dictionary<int, Player> _spectateTargets = new();
    private bool _disposed;

    public PlayerHUD()
    {
        Exiled.Events.Handlers.Player.Verified += ServerInfoHint;
        Exiled.Events.Handlers.Server.RoundStarted += PlayerHUDMain;
        Exiled.Events.Handlers.Player.ChangingRole += AllSyncHUD;
        Exiled.Events.Handlers.Server.RoundStarted += AllSyncHUD_;
        Exiled.Events.Handlers.Server.RestartingRound += DestroyHints;
        Exiled.Events.Handlers.Player.ChangingSpectatedPlayer += Spectate;
        Exiled.Events.Handlers.Player.Left += OnLeft;

        // 旧仕様と同じく、コルーチンはプラグイン生存中ずっと回す
        _specificAbilityLoop = Timing.RunCoroutine(SpecificInfoHudLoop());
        _abilityHudLoop = Timing.RunCoroutine(AbilityHudLoop());
        _taskSyncLoop = Timing.RunCoroutine(TaskSync());
        _debugHudLoop = Timing.RunCoroutine(DebugHudLoop());
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Exiled.Events.Handlers.Player.Verified -= ServerInfoHint;
        Exiled.Events.Handlers.Server.RoundStarted -= PlayerHUDMain;
        Exiled.Events.Handlers.Player.ChangingRole -= AllSyncHUD;
        Exiled.Events.Handlers.Server.RoundStarted -= AllSyncHUD_;
        Exiled.Events.Handlers.Server.RestartingRound -= DestroyHints;
        Exiled.Events.Handlers.Player.ChangingSpectatedPlayer -= Spectate;
        Exiled.Events.Handlers.Player.Left -= OnLeft;

        if (_specificAbilityLoop.IsRunning)
            Timing.KillCoroutines(_specificAbilityLoop);

        if (_abilityHudLoop.IsRunning)
            Timing.KillCoroutines(_abilityHudLoop);

        if (_taskSyncLoop.IsRunning)
            Timing.KillCoroutines(_taskSyncLoop);
        
        if (_debugHudLoop.IsRunning)
            Timing.KillCoroutines(_debugHudLoop);

        _spectateTargets.Clear();
        GC.SuppressFinalize(this);
    }


    // =========================================================
    // ヘルパー
    // =========================================================

    /// <summary>プレイヤーが安全に操作できる状態かどうか確認する</summary>
    private static bool IsPlayerValid(Player? p)
    {
        try
        {
            return p != null && p.IsConnected && p.ReferenceHub != null;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasReferenceHub(Player? p)
    {
        try
        {
            return p?.ReferenceHub != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>PlayerDisplay を安全に取得する。失敗時は null を返す</summary>
    private static PlayerDisplay? TryGetDisplay(Player p)
    {
        try
        {
            return PlayerDisplay.Get(p.ReferenceHub);
        }
        catch
        {
            return null;
        }
    }

    // =========================================================
    // ServerInfoHint / Setup / Main
    // =========================================================

    public void ServerInfoHint(VerifiedEventArgs? ev)
    {
        if (ev?.Player == null) return; // FIX: nullガード

        var display = TryGetDisplay(ev.Player);
        if (display == null) return;

        EnsureServerInfoHint(display);

        // ラウンド中に途中参加した場合は HUD も作る + ロール同期
        if (!Round.IsLobby)
        {
            PlayerHUDSetup(ev.Player);
            ApplyRoleInfo(ev.Player, ev.Player);
        }
    }

    private void PlayerHUDSetup(Player player)
    {
        if (!IsPlayerValid(player)) return; // FIX: nullガード

        var display = TryGetDisplay(player);
        if (display == null) return;

        int XCordinate = -350;

        EnsureServerInfoHint(display);
        EnsurePlayerHudHint(display, HudConstId.PlayerHUD_Role, "Role: " + player.CustomInfo, HintAlignment.Left, HintSyncSpeed.Fastest, 24, XCordinate, 860);
        EnsurePlayerHudHint(display, HudConstId.PlayerHUD_Objective, "Objective: Undefined", HintAlignment.Left, HintSyncSpeed.Fastest, 30, XCordinate, 915);
        EnsurePlayerHudHint(display, HudConstId.PlayerHUD_Team, "Team: Undefined", HintAlignment.Left, HintSyncSpeed.Fastest, 24, XCordinate, 885);
        EnsurePlayerHudHint(display, HudConstId.PlayerHUD_Event, "[Event]\n<size=28>Undefined</size>", HintAlignment.Left, HintSyncSpeed.Fast, 26, XCordinate, 120);
        EnsurePlayerHudHint(display, HudConstId.PlayerHUD_Specific, string.Empty, HintAlignment.Left, HintSyncSpeed.Fastest, 24, XCordinate + 350, 880);
        EnsurePlayerHudHint(display, HudConstId.PlayerHUD_Ability, string.Empty, HintAlignment.Left, HintSyncSpeed.Fastest, 22, XCordinate + 350, 825);
        EnsurePlayerHudHint(display, HudConstId.PlayerHUD_EffectedInfo, string.Empty, HintAlignment.Center, HintSyncSpeed.Fastest, 24, 0, 915);
        EnsurePlayerHudHint(display, HudConstId.PlayerHUD_Debug, string.Empty, HintAlignment.Left, HintSyncSpeed.Fast, 24, XCordinate, 345);
    }

    private static string BuildServerInfoText()
    {
        return Plugin.Singleton.Config.IsBeta
            ? "[<color=#008cff>Sharp Server</color> - <color=red>BETA</color>]"
            : "[<color=#008cff>Sharp Server</color>]";
    }

    private static HintServiceMeow.Core.Models.Hints.AbstractHint EnsureServerInfoHint(PlayerDisplay display)
    {
        var existing = display.GetHint(HudConstId.PlayerHUD_ServerInfo);
        if (existing != null)
        {
            existing.Text = BuildServerInfoText();
            return existing;
        }

        var hint = new Hint
        {
            Id = HudConstId.PlayerHUD_ServerInfo,
            Alignment = HintAlignment.Center,
            SyncSpeed = HintSyncSpeed.UnSync,
            FontSize = 18,
            XCoordinate = 0,
            YCoordinate = 1050,
            ResolutionBasedAlign = true
        };
        hint.Text = BuildServerInfoText();
        display.AddHint(hint);
        return hint;
    }

    private static void EnsurePlayerHudHint(
        PlayerDisplay display,
        string id,
        string defaultText,
        HintAlignment alignment,
        HintSyncSpeed syncSpeed,
        int fontSize,
        int x,
        int y)
    {
        if (display.GetHint(id) is not Hint hint)
        {
            hint = new Hint
            {
                Id = id,
                Text = defaultText,
                Alignment = alignment,
                SyncSpeed = syncSpeed,
                FontSize = fontSize,
                XCoordinate = x,
                YCoordinate = y,
                ResolutionBasedAlign = true
            };
            display.AddHint(hint);
        }

        if (string.IsNullOrEmpty(hint.Text))
            hint.Text = defaultText;

        hint.Alignment = alignment;
        hint.SyncSpeed = syncSpeed;
        hint.FontSize = fontSize;
        hint.XCoordinate = x;
        hint.YCoordinate = y;
        hint.ResolutionBasedAlign = true;
    }

    public void PlayerHUDMain()
    {
        // 旧仕様寄り：RoundStarted 時点で全員分 HUD 作成
        foreach (Player player in Player.List.ToList()) // FIX: ToList()
        {
            if (!IsPlayerValid(player)) continue;
            PlayerHUDSetup(player);
            ApplyRoleInfo(player, player);
        }
    }

    // =========================================================
    // HintSync
    // =========================================================

    public void HintSync(SyncType syncType, string hintText, Player player)
    {
        if (!IsPlayerValid(player)) return; // FIX: nullガード

        var display = TryGetDisplay(player);
        if (display == null) return;

        try
        {
            switch (syncType)
            {
                case SyncType.ServerInfo:
                    var si = EnsureServerInfoHint(display);
                    si.Text = hintText;
                    break;
                case SyncType.PHUD_Role:
                    var role = display.GetHint(HudConstId.PlayerHUD_Role);
                    if (role != null) role.Text = "Role: " + hintText;
                    break;
                case SyncType.PHUD_Objective:
                    var obj = display.GetHint(HudConstId.PlayerHUD_Objective);
                    if (obj != null) obj.Text = "Objective: " + hintText;
                    break;
                case SyncType.PHUD_Team:
                    var team = display.GetHint(HudConstId.PlayerHUD_Team);
                    if (team != null) team.Text = "Team: " + hintText;
                    break;
                case SyncType.PHUD_Event:
                    var ev = display.GetHint(HudConstId.PlayerHUD_Event);
                    if (ev != null) ev.Text = "[Event]\n<size=28>" + hintText + "</size>";
                    break;
                case SyncType.PHUD_Specific:
                    var specific = display.GetHint(HudConstId.PlayerHUD_Specific);
                    if (specific != null) specific.Text = hintText;
                    break;
                case SyncType.PHUD_Ability:
                    var ab = display.GetHint(HudConstId.PlayerHUD_Ability);
                    if (ab != null) ab.Text = hintText;
                    break;
                case SyncType.PHUD_EffectedInfo:
                    var effected = display.GetHint(HudConstId.PlayerHUD_EffectedInfo);
                    if (effected != null) effected.Text = hintText;
                    break;
                case SyncType.PHUD_Debug:
                    var db = display.GetHint(HudConstId.PlayerHUD_Debug);
                    if (db != null) db.Text = hintText;
                    break;
            }
        }
        catch (Exception)
        {
            // Log.Debug($"[HintSync] Exception for {player.Nickname}: {e.Message}");
        }
    }

    // =========================================================
    // ロール情報構築
    // =========================================================

    private void ApplyRoleInfo(Player sourcePlayer, Player targetForHint)
    {
        if (!IsPlayerValid(sourcePlayer)) return;
        if (!IsPlayerValid(targetForHint)) return;

        try
        {
            string roleText, teamText, objectiveText;

            // FacilityTermination 中は勝利条件と同じ人類/正常性の陣営表示に寄せる
            if (SpecialEventsHandler.Instance.NowEvent == SpecialEventType.FacilityTermination)
            {
                (roleText, teamText, objectiveText) = GetFacilityTerminationInfo(sourcePlayer);
                HintSync(SyncType.PHUD_Role,      roleText,      targetForHint);
                HintSync(SyncType.PHUD_Team,      teamText,      targetForHint);
                HintSync(SyncType.PHUD_Objective, objectiveText, targetForHint);
                HintSync(SyncType.PHUD_Event,     SpecialEventsHandler.Instance.LocalizedEventName, targetForHint);
                return;
            }

            var custom = sourcePlayer.GetCustomRole();

            if (custom != CRoleTypeId.None &&
                RoleHintsDictionary.Table.TryGetValue(custom, out var data))
            {
                roleText      = data.Role;
                teamText      = data.Team;
                objectiveText = data.Objective;
            }
            else
            {
                (roleText, teamText, objectiveText) = GetTeamFallback(sourcePlayer);
            }

            HintSync(SyncType.PHUD_Role,      roleText,      targetForHint);
            HintSync(SyncType.PHUD_Objective, objectiveText, targetForHint);
            HintSync(SyncType.PHUD_Team,      teamText,      targetForHint);
            HintSync(SyncType.PHUD_Event,     SpecialEventsHandler.Instance.LocalizedEventName, targetForHint);
        }
        catch (Exception e)
        {
            Log.Debug($"[ApplyRoleInfo] Exception for {sourcePlayer?.Nickname}: {e.Message}");
        }
    }

    private static (string role, string team, string objective) GetFacilityTerminationInfo(Player player)
    {
        var custom = player.GetCustomRole();
        var cteam = player.GetTeam();
        var name = player.Role?.Name ?? "";

        var roleText = custom != CRoleTypeId.None &&
                       RoleHintsDictionary.Table.TryGetValue(custom, out var data)
            ? data.Role
            : $"<color={(player.IsHumanitist() ? "#0000c8" : "red")}>{name}</color>";

        if (!player.IsHumanitist())
        {
            if (custom == CRoleTypeId.Sculpture)
                roleText = "<color=red>Sculpture</color>";
            else if (cteam is CTeam.FoundationForces or CTeam.Guards)
                roleText = $"<color=red>{name}</color>";

            return (
                roleText,
                "<color=red>正常性</color>",
                "財団に従い、人類を根絶させよ。"
            );
        }

        var objective = cteam is CTeam.Scientists or CTeam.ClassD
            ? "施設の方針変更に巻き込まれた。生き延び、正常性陣営から逃れろ。"
            : "人類第一に、正常性陣営に抵抗せよ。";

        return (
            roleText,
            "<color=#0000c8>人類</color>",
            objective
        );
    }

    private static (string role, string team, string objective) GetTeamFallback(Player player)
    {
        if (!IsPlayerValid(player))
            return ("<color=#ffffff></color>", "<color=#ffffff>[Unknown]</color>", "[Unknown]");

        string name = player.Role?.Name ?? "";
        return player.Role?.Team switch
        {
            Team.ClassD          => ($"<color=#ee7600>{name}</color>", "<color=#ee7600>Neutral - Side Chaos</color>",       "施設から脱出せよ"),
            Team.Scientists      => ($"<color=#faff86>{name}</color>", "<color=#faff86>Neutral - Side Foundation</color>",  "施設から脱出せよ"),
            Team.ChaosInsurgency => ($"<color=#228b22>{name}</color>", "<color=#228b22>Chaos Insurgency</color>",           "Dクラス職員を救出し、施設を略奪せよ。"),
            Team.FoundationForces=> ($"<color=#00b7eb>{name}</color>", "<color=#00b7eb>The Foundation</color>",             "研究員を救出し、施設の秩序を守護せよ。"),
            Team.SCPs            => ($"<color=#c50000>{name}</color>", "<color=#c50000>The SCPs</color>",                   "己の本能・復讐心と利益の為に動け"),
            Team.Flamingos       => ($"<color=#ff96de>{name}</color>", "<color=#ff96de>The Flamingos</color>",              "フラミンゴ！"),
            _                    => ($"<color=#ffffff>{name}</color>", "<color=#ffffff>[Unknown]</color>",                  "[Unknown]"),
        };
    }

    // =========================================================
    // 全体同期
    // =========================================================

    public void SyncTexts(Player? spectator = null, Player? spectatedTarget = null)
    {
        // 両方 null → 全員分を自分自身で同期
        if (spectator is null && spectatedTarget is null)
        {
            foreach (Player player in Player.List.ToList()) // FIX: ToList()
            {
                if (!IsPlayerValid(player)) continue;
                if (player.Role?.Team == Team.Dead) continue;

                ApplyRoleInfo(player, player);
            }
        }
        // 観戦者 + 対象が両方 not null → 対象の情報を観戦者に同期
        else if (spectator is not null && spectatedTarget is not null)
        {
            if (!IsPlayerValid(spectatedTarget)) return; // FIX: IsPlayerValidで一括確認
            if (spectatedTarget.Role?.Team == Team.Dead) return;

            ApplyRoleInfo(spectatedTarget, spectator);
        }
    }

    public void AllSyncHUD(ChangingRoleEventArgs? ev)
    {
        if (ev?.Player == null) return;
        if (!ev.IsAllowed) return;

        var playerId = ev.Player.Id;

        Timing.CallDelayed(0.5f, () =>
        {
            var player = Player.List.FirstOrDefault(p => p?.Id == playerId);
            if (player == null || !IsPlayerValid(player)) return; // FIX: 遅延後の生存確認
            if (player.Role?.Team == Team.Dead) return;
            ApplyRoleInfo(player, player);
        });
    }

    public void AllSyncHUD_()
    {
        SyncTexts();
    }

    public void ForceUpdateAll() => AllSyncHUD_();

    public void ForceAbilityHudSync(Player player)
    {
        if (!IsPlayerValid(player)) return;

        var display = TryGetDisplay(player);
        if (display == null) return;

        var abilityHint = display.GetHint(HudConstId.PlayerHUD_Ability);
        if (abilityHint == null)
        {
            PlayerHUDSetup(player);
            abilityHint = display.GetHint(HudConstId.PlayerHUD_Ability);
            if (abilityHint == null) return;
        }

        abilityHint.Text = BuildAbilityHud(player);
    }

    // =========================================================
    // 観戦時の同期
    // =========================================================

    public void Spectate(ChangingSpectatedPlayerEventArgs? ev)
    {
        // FIX: ev・spectator の nullガード
        if (ev?.Player == null) return;
        var spectator = ev.Player;
        if (!IsPlayerValid(spectator)) return;

        // 観戦解除（NewTarget が null）
        if (ev.NewTarget == null)
        {
            _spectateTargets.Remove(spectator.Id);

            // 自分自身の HUD を戻す
            if (IsPlayerValid(spectator) && spectator.Role?.Team != Team.Dead)
                ApplyRoleInfo(spectator, spectator);

            return;
        }

        var target = ev.NewTarget;

        // FIX: ターゲットの安全確認
        if (!IsPlayerValid(target)) return;

        _spectateTargets[spectator.Id] = target;

        // 1. ロール HUD 同期
        SyncTexts(spectator, target);

        // FIX: PlayerDisplay 取得を安全なヘルパーで実施
        var display = TryGetDisplay(spectator);
        if (display == null) return;

        // 2. Specific HUD 即時同期
        var specificHint = display.GetHint("PlayerHUD_Specific");
        if (specificHint != null)
        {
            try
            {
                specificHint.Text = RoleSpecificTextProvider.GetFor(target);
            }
            catch (Exception e)
            {
                Log.Debug($"[Spectate] Specific hint error: {e.Message}");
            }
        }

        // 3. Ability HUD 即時同期
        var abilityHint = display.GetHint("PlayerHUD_Ability");
        if (abilityHint != null)
        {
            try
            {
                abilityHint.Text = BuildAbilityHud(target);
            }
            catch (Exception e)
            {
                Log.Debug($"[Spectate] Ability hint error: {e.Message}");
            }
        }
    }

    private void OnLeft(LeftEventArgs? ev)
    {
        if (ev?.Player == null)
            return;

        int playerId = ev.Player.Id;
        _spectateTargets.Remove(playerId);

        foreach (var spectatorId in _spectateTargets
                     .Where(x => x.Value.Id == playerId || !HasReferenceHub(x.Value))
                     .Select(x => x.Key)
                     .ToList())
        {
            _spectateTargets.Remove(spectatorId);
        }
    }

    // =========================================================
    // DestroyHints
    // =========================================================

    public void DestroyHints()
    {
        foreach (Player player in Player.List.ToList()) // FIX: ToList()
        {
            if (!IsPlayerValid(player)) continue; // FIX: nullガード
            try
            {
                var display = TryGetDisplay(player);
                display?.ClearHint();
            }
            catch (Exception e)
            {
                Log.Debug($"[DestroyHints] Exception for {player?.Nickname}: {e.Message}");
            }
        }

        _spectateTargets.Clear();

        Timing.CallDelayed(RoleSpawnTimings.HudRecreateAfterClear, () =>
        {
            foreach (Player player in Player.List.ToList())
            {
                if (!IsPlayerValid(player)) continue;

                var display = TryGetDisplay(player);
                if (display == null) continue;

                EnsureServerInfoHint(display);

                if (!Round.IsLobby)
                {
                    PlayerHUDSetup(player);
                    if (player.Role?.Team != Team.Dead)
                        ApplyRoleInfo(player, player);
                }
            }
        });

        // ★ コルーチンは止めない（旧仕様の安定性維持）
    }

    // =========================================================
    // Ability HUD
    // =========================================================

    private string BuildAbilityHud(Player target)
    {
        if (!IsPlayerValid(target)) return string.Empty; // FIX: nullガード
        if (!target.IsAlive) return string.Empty;

        if (!AbilityManager.TryGetLoadout(target, out var loadout))
            return string.Empty;

        var entries = GetAbilityEntries(loadout);
        if (entries.Count == 0)
            return string.Empty;

        var activeEntryIndex = entries.FindIndex(e => e.SlotIndex == loadout.ActiveIndex);
        if (activeEntryIndex < 0)
        {
            loadout.ActiveIndex = entries[0].SlotIndex;
            activeEntryIndex = 0;
        }

        var active = entries[activeEntryIndex].Ability;
        var abilityName = AbilityLocalization.GetDisplayName(active.GetType().Name, target);
        var statusText = FormatAbilityState(target, active, out var usesText);
        var countText = $"{activeEntryIndex + 1}/{entries.Count}";
        var controlText = entries.Count > 1
            ? "使用:アビリティ使用 / 切替:アビリティ切替"
            : "使用:アビリティ使用 / 所持:1";
        var slotSummary = BuildAbilitySlotSummary(target, entries, activeEntryIndex);

        return $"<size=22><color=#ffcc00>Ability {countText}</color> {abilityName} {statusText} Uses:{usesText}</size>\n" +
               $"<size=18>{controlText} | {slotSummary}</size>";
    }

    private static List<(int SlotIndex, AbilityBase Ability)> GetAbilityEntries(AbilityLoadout loadout)
    {
        var entries = new List<(int SlotIndex, AbilityBase Ability)>();

        for (var i = 0; i < AbilityLoadout.MaxSlots; i++)
        {
            var ability = loadout.Slots[i];
            if (ability != null)
                entries.Add((i, ability));
        }

        return entries;
    }

    private static string BuildAbilitySlotSummary(
        Player player,
        IReadOnlyList<(int SlotIndex, AbilityBase Ability)> entries,
        int activeEntryIndex)
    {
        var sb = new StringBuilder();

        for (var i = 0; i < entries.Count; i++)
        {
            if (i > 0)
                sb.Append(" | ");

            var ability = entries[i].Ability;
            var marker = i == activeEntryIndex
                ? $"<color=#ffcc00>*{i + 1}</color>"
                : (i + 1).ToString();
            var name = ShortenAbilityName(
                AbilityLocalization.GetDisplayName(ability.GetType().Name, player),
                8);

            sb.Append(marker)
                .Append(':')
                .Append(name)
                .Append(' ')
                .Append(FormatCompactAbilityState(player, ability));
        }

        return sb.ToString();
    }

    private static string FormatAbilityState(
        Player player,
        AbilityBase ability,
        out string usesText)
    {
        usesText = "?";

        if (!AbilityBase.TryGetAbilityState(
                player,
                ability,
                out var canUse,
                out var cdRemain,
                out var usesLeft,
                out var maxUses))
            return "<color=#aaaaaa>?</color>";

        usesText = maxUses < 0 ? "∞" : Math.Max(0, usesLeft).ToString();

        if (maxUses >= 0 && usesLeft <= 0)
            return "<color=#ff6666>DONE</color>";

        return canUse
            ? "<color=#38ff6b>READY</color>"
            : $"<color=#ffd966>CD {Mathf.CeilToInt(cdRemain)}s</color>";
    }

    private static string FormatCompactAbilityState(Player player, AbilityBase ability)
    {
        if (!AbilityBase.TryGetAbilityState(
                player,
                ability,
                out var canUse,
                out var cdRemain,
                out var usesLeft,
                out var maxUses))
            return "<color=#aaaaaa>?</color>";

        if (maxUses >= 0 && usesLeft <= 0)
            return "<color=#ff6666>0</color>";

        return canUse
            ? "<color=#38ff6b>OK</color>"
            : $"<color=#ffd966>{Mathf.CeilToInt(cdRemain)}s</color>";
    }

    private static string ShortenAbilityName(string name, int maxLength)
    {
        if (string.IsNullOrEmpty(name) || name.Length <= maxLength)
            return name;

        return name.Substring(0, Math.Max(1, maxLength - 3)) + "...";
    }

    // =========================================================
    // コルーチン
    // =========================================================

    private IEnumerator<float> TaskSync()
    {
        yield return Timing.WaitForSeconds(2f);

        for (;;)
        {
            if (Round.IsLobby)
            {
                yield return Timing.WaitForSeconds(1f);
                continue;
            }

            SyncTexts();
            yield return Timing.WaitForSeconds(3f);
        }
    }

    private IEnumerator<float> AbilityHudLoop()
    {
        yield return Timing.WaitForSeconds(0.5f);

        for (;;)
        {
            if (Round.IsLobby)
            {
                yield return Timing.WaitForSeconds(0.5f);
                continue;
            }

            foreach (var player in Player.List.ToList()) // FIX: ToList()
            {
                // FIX: IsPlayerValid で一括確認
                if (!IsPlayerValid(player)) continue;

                var display = TryGetDisplay(player);
                if (display == null) continue;

                var abilityHint = display.GetHint("PlayerHUD_Ability");
                if (abilityHint == null)
                {
                    PlayerHUDSetup(player);
                    abilityHint = display.GetHint("PlayerHUD_Ability");
                    if (abilityHint == null) continue;
                }

                // 観戦者ならターゲット側の Ability を見る
                var hudTarget = player;
                if (player.Role?.Team == Team.Dead &&
                    _spectateTargets.TryGetValue(player.Id, out var t) &&
                    IsPlayerValid(t) && t.IsAlive) // FIX: IsPlayerValid で一括確認
                    hudTarget = t;

                try
                {
                    abilityHint.Text = BuildAbilityHud(hudTarget);
                }
                catch (Exception e)
                {
                    Log.Debug($"[AbilityHudLoop] Exception for {player.Nickname}: {e.Message}");
                }
            }

            yield return Timing.WaitForSeconds(0.5f);
        }
    }

    private IEnumerator<float> SpecificInfoHudLoop()
    {
        yield return Timing.WaitForSeconds(1f);

        for (;;)
        {
            if (Round.IsLobby)
            {
                yield return Timing.WaitForSeconds(1f);
                continue;
            }

            foreach (var player in Player.List.ToList()) // FIX: ToList()
            {
                if (!IsPlayerValid(player)) continue; // FIX: IsPlayerValid で一括確認

                // 観戦者ならターゲット側の情報を見る
                var hudTarget = player;
                if (player.Role?.Team == Team.Dead &&
                    _spectateTargets.TryGetValue(player.Id, out var t) &&
                    IsPlayerValid(t) && t.IsAlive) // FIX: IsPlayerValid で一括確認
                    hudTarget = t;

                var display = TryGetDisplay(player);
                if (display == null) continue;

                var specificHint = display.GetHint("PlayerHUD_Specific");
                if (specificHint == null)
                {
                    PlayerHUDSetup(player);
                    specificHint = display.GetHint("PlayerHUD_Specific");
                    if (specificHint == null) continue;
                }

                try
                {
                    string roleSpecific = RoleSpecificTextProvider.GetFor(hudTarget);

                    specificHint.Text = string.IsNullOrEmpty(roleSpecific)
                        ? string.Empty
                        : roleSpecific;
                }
                catch (Exception e)
                {
                    Log.Debug($"[SpecificInfoHudLoop] Exception for {player.Nickname}: {e.Message}");
                }
            }

            yield return Timing.WaitForSeconds(1f);
        }
    }
    
    /// <summary>
    /// デバッグモード ON のプレイヤーに対して 0.1 秒ごとに
    /// PHUD_Debug ヒントを更新するループ。
    /// </summary>
    private IEnumerator<float> DebugHudLoop()
    {
        yield return Timing.WaitForSeconds(0.5f);
 
        for (;;)
        {
            if (Round.IsLobby)
            {
                yield return Timing.WaitForSeconds(0.5f);
                continue;
            }
 
            foreach (var player in Player.List.ToList())
            {
                if (!IsPlayerValid(player)) continue;
                if (!DebugModeHandler.IsDebugMode(player)) continue;
 
                var display = TryGetDisplay(player);
                if (display == null) continue;
 
                var hint = display.GetHint("PlayerHUD_Debug");
                if (hint == null)
                {
                    PlayerHUDSetup(player);
                    hint = display.GetHint("PlayerHUD_Debug");
                    if (hint == null) continue;
                }
 
                try
                {
                    hint.Text = BuildDebugHud(player);
                }
                catch (Exception)
                {
                    // Log.Debug($"[DebugHudLoop] Exception for {player.Nickname}: {e.Message}");
                }
            }
 
            yield return Timing.WaitForSeconds(0.1f);
        }
    }
    
    private static string BuildDebugHud(Player player)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<size=18><color=#ffff00>[DEBUG MODE]</color>");

        // ── ロール・チーム情報 ────────────────────────────────────────
        sb.AppendLine(
            $"<color=#aaaaaa>Role:</color> {player.Role?.Name ?? "None"}  " +
            $"<color=#aaaaaa>Team:</color> {player.Role?.Team.ToString() ?? "None"}  " +
            $"<color=#aaaaaa>CRole:</color> {player.GetCustomRole()}  " +
            $"<color=#aaaaaa>CTeam:</color> {player.GetTeam()}"
        );

        // ── 座標・ルーム情報（リアルタイム） ─────────────────────────
        var pos  = player.Position;
        var room = player.CurrentRoom;
        sb.AppendLine(
            $"<color=#aaaaaa>World:</color> ({pos.x:F2}, {pos.y:F2}, {pos.z:F2})  " +
            $"<color=#aaaaaa>Room:</color> {room?.Type.ToString() ?? "None"} " +
            $"<color=#aaaaaa>Zone:</color> {player.Zone.ToString()}"
        );
        if (room != null)
        {
            var invRot     = Quaternion.Inverse(room.Rotation);
            var localPos   = invRot * (pos - room.Position);
            var localEuler = invRot.eulerAngles;
            var roomEuler  = room.Rotation.eulerAngles;
            sb.AppendLine(
                $"<color=#aaaaaa>Local:</color> ({localPos.x:F2}, {localPos.y:F2}, {localPos.z:F2})  " +
                $"<color=#aaaaaa>LocalRot:</color> ({localEuler.x:F1}, {localEuler.y:F1}, {localEuler.z:F1})"
            );
            sb.AppendLine(
                $"<color=#aaaaaa>RoomRot:</color> ({roomEuler.x:F1}, {roomEuler.y:F1}, {roomEuler.z:F1})"
            );
        }

        // ── 最後に触ったドア情報 ─────────────────────────────────────
        if (DebugModeHandler.TryGetDoor(player, out var door))
        {
            sb.AppendLine(
                $"<color=#aaaaaa>Door:</color> {door.DoorType}  " +
                $"<color=#aaaaaa>Name:</color> {door.DoorName}  " +
                $"<color=#aaaaaa>Room:</color> {door.RoomType}"
            );
            sb.AppendLine(
                $"<color=#aaaaaa>DoorLocal:</color> ({door.LocalPos.x:F2}, {door.LocalPos.y:F2}, {door.LocalPos.z:F2})  " +
                $"<color=#aaaaaa>DoorRot:</color> ({door.LocalEuler.x:F1}, {door.LocalEuler.y:F1}, {door.LocalEuler.z:F1})"
            );
            sb.AppendLine(
                $"<color=#aaaaaa>DoorRoomRot:</color> ({door.RoomEuler.x:F1}, {door.RoomEuler.y:F1}, {door.RoomEuler.z:F1})"
            );
        }
        else
        {
            sb.AppendLine("<color=#666666>Door: -- (ドアに触れると更新)</color>");
        }

        // ── ラウンド状態フラグ ────────────────────────────────────────
        static string Bool(bool v) => v ? "<color=green>T</color>" : "<color=red>F</color>";
        sb.AppendLine(
            $"<color=#aaaaaa>Round:</color> " +
            $"InProgress={Bool(Round.InProgress)}  " +
            $"IsStarted={Bool(Round.IsStarted)}  " +
            $"IsEnded={Bool(Round.IsEnded)}  " +
            $"IsLobby={Bool(Round.IsLobby)}  " +
            $"IsLocked={Bool(Round.IsLocked)}  " +
            $"IsLobbyLocked={Bool(Round.IsLobbyLocked)}"
        );
        sb.AppendLine(
            $"<color=#aaaaaa>Elapsed:</color> {Round.ElapsedTime:mm\\:ss}  " +
            $"<color=#aaaaaa>UptimeRounds:</color> {Round.UptimeRounds}  " +
            $"<color=#aaaaaa>All Players:</color> {Player.List.Count} " +
            $"<color=#aaaaaa>Connected Players:</color> {Player.List.Count(p => !p.IsNPC && !p.IsHost)} " +
            $"<color=#aaaaaa>Npcs:</color> {Npc.List.Count} "
        );

        // ── 核弾頭タイマー情報 ───────────────────────────────────────
        if (Warhead.IsInProgress)
        {
            sb.AppendLine(
                $"<color=#ff4444>Warhead:</color> " +
                $"DetonationTimer={Warhead.DetonationTimer:F1}  " +
                $"RealTimer={Warhead.RealDetonationTimer:F1}  " +
                $"IsLocked={Bool(Warhead.IsLocked)} " +
                $"IsBooming={Bool(MapFlags.IsWarheadBooming)} "
            );
        }
        else
        {
            sb.AppendLine("<color=#666666>Warhead: Not active</color>");
        }
        
        // ── 装備中アイテム情報 ─────────────────────────────────────────
        var currentItem = player.CurrentItem;
        if (currentItem == null)
        {
            sb.AppendLine("<color=#666666>Item: -- (未装備)</color>");
        }
        else
        {
            sb.AppendLine(
                $"<color=#aaaaaa>Item:</color> {currentItem.Type}  " +
                $"<color=#aaaaaa>Serial:</color> {currentItem.Serial}  " +
                $"<color=#aaaaaa>Category:</color> {currentItem.Category}  " +
                $"<color=#aaaaaa>Weight:</color> {currentItem.Weight:F2}"
            );

            // ── CItem 情報 ─────────────────────────────────────────────
            if (CItem.TryGet(currentItem, out var cItem) && cItem != null)
            {
                sb.AppendLine(
                    $"<color=#88ffcc>[CItem]</color> " +
                    $"<color=#aaaaaa>Key:</color> {cItem.UniqueKeyName}  " +
                    $"<color=#aaaaaa>Type:</color> {cItem.GetType().Name}  " +
                    $"<color=#aaaaaa>Display:</color> {cItem.DisplayName}"
                );
                string desc = cItem.Description;
                if (!string.IsNullOrEmpty(desc))
                    sb.AppendLine($"  <color=#aaaaaa>Desc:</color> {desc}");

                // ★ Hybrid なら内部トラッキング状態も出す
                if (cItem is CItemHybrid hybrid)
                {
                    try
                    {
                        sb.AppendLine(hybrid.GetDebugStateFor(player, currentItem.Serial));
                    }
                    catch (Exception e)
                    {
                        sb.AppendLine($"<color=#ff4444>[Hybrid Debug Error]</color> {e.Message}");
                    }
                }
            }

            // ── Firearm 情報 ───────────────────────────────────────────
            if (currentItem is Exiled.API.Features.Items.Firearm firearm)
            {
                sb.AppendLine(
                    $"<color=#ffaa44>[Firearm]</color> " +
                    $"<color=#aaaaaa>Type:</color> {firearm.FirearmType}  " +
                    $"<color=#aaaaaa>Ammo:</color> {firearm.MagazineAmmo}/{firearm.MaxMagazineAmmo}  " +
                    $"<color=#aaaaaa>Barrel:</color> {firearm.BarrelAmmo}/{firearm.MaxBarrelAmmo}  " +
                    $"<color=#aaaaaa>Total:</color> {firearm.TotalAmmo}"
                );
                sb.AppendLine(
                    $"  <color=#aaaaaa>Dmg:</color> {firearm.Damage:F1}  " +
                    $"<color=#aaaaaa>EffDmg:</color> {firearm.EffectiveDamage:F1}  " +
                    $"<color=#aaaaaa>Pen:</color> {firearm.Penetration:F2}  " +
                    $"<color=#aaaaaa>Inaccuracy:</color> {firearm.Inaccuracy:F3}  " +
                    $"<color=#aaaaaa>Falloff:</color> {firearm.DamageFalloffDistance:F1}"
                );
                sb.AppendLine(
                    $"  <color=#aaaaaa>Auto:</color> {Bool(firearm.IsAutomatic)}  " +
                    $"<color=#aaaaaa>Aiming:</color> {Bool(firearm.Aiming)}  " +
                    $"<color=#aaaaaa>Reloading:</color> {Bool(firearm.IsReloading)}  " +
                    $"<color=#aaaaaa>NV:</color> {Bool(firearm.NightVisionEnabled)}  " +
                    $"<color=#aaaaaa>Light:</color> {Bool(firearm.FlashlightEnabled)}"
                );
                // アタッチメント一覧
                var attachments = firearm.AttachmentIdentifiers.ToList();
                if (attachments.Count == 0)
                {
                    sb.AppendLine("  <color=#666666>Attachments: None</color>");
                }
                else
                {
                    sb.Append("  <color=#aaaaaa>Attachments:</color>");
                    foreach (var att in attachments)
                        sb.Append($" <color=#dddd88>{att.Name}</color>");
                    sb.AppendLine();
                }
            }

            // ── Armor 情報 ─────────────────────────────────────────────
            if (currentItem is Exiled.API.Features.Items.Armor armor)
            {
                sb.AppendLine(
                    $"<color=#aaddff>[Armor]</color> " +
                    $"<color=#aaaaaa>Vest:</color> {armor.VestEfficacy}  " +
                    $"<color=#aaaaaa>Helmet:</color> {armor.HelmetEfficacy}  " +
                    $"<color=#aaaaaa>Stamina×:</color> {armor.StaminaUseMultiplier:F2}"
                );
            }

            // ── インベントリ全体の簡易サマリ ──────────────────────────
            var items = player.Items.ToList();
            sb.Append($"<color=#aaaaaa>Inventory ({items.Count}):</color>");
            foreach (var it in items)
            {
                bool isCurrent = it.Serial == currentItem.Serial;
                bool isCItem   = CItem.TryGet(it, out var itCi);
                string tag     = isCItem ? $"<color=#88ffcc>[C]</color>" : "";
                string cur     = isCurrent ? "<color=yellow>▶</color>" : "  ";
                sb.Append($" {cur}{tag}{it.Type}");
            }
            sb.AppendLine();
        }
        
        // ── 有効なエフェクト一覧 ─────────────────────────────────────
        var activeEffects = player.ActiveEffects?.ToList();
        if (activeEffects is null || activeEffects?.Count == 0)
        {
            sb.AppendLine("<color=#666666>Effects: None</color>");
        }
        else
        {
            sb.AppendLine("<color=#aaaaaa>Effects:</color>");
            foreach (var effect in activeEffects!)
            {
                if (effect is null) continue;
                string duration = effect.Duration > 0f
                    ? $"{effect.TimeLeft:F0}"
                    : "∞";
                sb.AppendLine(
                    $"- <color=#88ddff>{effect.GetType().Name,-24}</color>" +
                    $"| Intensity: {effect.Intensity,-3} Duration: {duration}"
                );
            }
        }

        // ── EXPERIMENTAL FEATURES ───────────────────────────────────
        var expectedTeam = RoundHandler.GetExpectedTeam();
        float elapsed = RoundHandler.ElapsedTime;
        float waitFor = RoundHandler.WaitForSpawnTime;
        float remaining = waitFor - elapsed;

        if (RoundHandler.IsAlreadySpawned)
        {
            sb.AppendLine(
                $"<color=#666666>FirstTeam: {expectedTeam} already spawned.</color>"
            );
        }
        else
        {
            string teamColor = RoundHandler.IsSecurityTeamExpected() ? "#00b7eb" : "#228b22";
            string urgency = remaining <= 30f ? "<color=#ff4444>" : "<color=#ffcc00>";
            sb.AppendLine(
                $"{urgency}FirstTeam:</color> " +
                $"<color={teamColor}>{expectedTeam}</color> spawns in " +
                $"{urgency}{remaining:F1}s</color> " +
                $"<color=#666666>({elapsed:F1} / {waitFor:F0})</color>"
            );
        }

        // ────────────────────────────────────────────────────────────
        // ★ 新しい項目はここに追加するだけでOK
        // 例:
        // sb.AppendLine($"<color=#aaaaaa>HP:</color> {player.Health:F0}/{player.MaxHealth:F0}");
        // ────────────────────────────────────────────────────────────

        sb.Append("</size>");
        return sb.ToString();
}
}
