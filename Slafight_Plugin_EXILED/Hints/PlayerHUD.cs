#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Models.HintContent;
using HintServiceMeow.Core.Models.Hints;
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
using Slafight_Plugin_EXILED.API.Interface;
using Hint = HintServiceMeow.Core.Models.Hints.Hint;

namespace Slafight_Plugin_EXILED.Hints;

public class PlayerHUD : IBootstrapHandler
{
    public static PlayerHUD Instance { get; private set; } = null!;
    public static void Register()   { Instance = new(); }
    public static void Unregister() { Instance = null!; }

    // 観戦者ID → 現在見ているプレイヤー
    private readonly Dictionary<int, Player> _spectateTargets = new();

    // =========================================================
    // 定数
    // =========================================================
    private const int LeftX  = -350;
    private const int RightX =    0;

    // 左カラム固定Y
    private const int Y_Role      = 870;
    private const int Y_Team      = 900;
    private const int Y_Objective = 930;
    private const int Y_Event     = 120;
    private const int Y_Debug     = 260;
    private const int Y_Server    = 1050;

    // 右カラム DynamicHint のターゲットY・境界
    // Specific と Ability は互いを自動回避する
    private const int Y_Specific_Target = 860;
    private const int Y_Ability_Target  = 900;

    // =========================================================
    // コンストラクタ / デストラクタ
    // =========================================================
    public PlayerHUD()
    {
        Exiled.Events.Handlers.Player.Verified           += OnVerified;
        Exiled.Events.Handlers.Server.RoundStarted       += OnRoundStarted;
        Exiled.Events.Handlers.Player.ChangingRole        += OnChangingRole;
        Exiled.Events.Handlers.Server.RestartingRound    += OnRestartingRound;
        Exiled.Events.Handlers.Player.ChangingSpectatedPlayer += OnSpectate;
    }

    ~PlayerHUD()
    {
        Exiled.Events.Handlers.Player.Verified           -= OnVerified;
        Exiled.Events.Handlers.Server.RoundStarted       -= OnRoundStarted;
        Exiled.Events.Handlers.Player.ChangingRole        -= OnChangingRole;
        Exiled.Events.Handlers.Server.RestartingRound    -= OnRestartingRound;
        Exiled.Events.Handlers.Player.ChangingSpectatedPlayer -= OnSpectate;
    }

    // =========================================================
    // ヘルパー
    // =========================================================
    private static bool IsValid(Player? p)
    {
        try { return p != null && p.IsConnected && p.ReferenceHub != null; }
        catch { return false; }
    }

    private static PlayerDisplay? GetDisplay(Player p)
    {
        try { return PlayerDisplay.Get(p.ReferenceHub); }
        catch { return null; }
    }

    /// <summary>観戦者なら観戦対象を、そうでなければ自分を返す</summary>
    private Player ResolveHudTarget(Player viewer)
    {
        if (viewer.Role?.Team == Team.Dead &&
            _spectateTargets.TryGetValue(viewer.Id, out var t) &&
            IsValid(t) && t.IsAlive)
            return t;
        return viewer;
    }

    // =========================================================
    // HUD セットアップ（プレイヤー1人分）
    // =========================================================
    private void SetupHUD(Player player)
    {
        if (!IsValid(player)) return;
        var display = GetDisplay(player);
        if (display == null) return;

        // 既にセットアップ済みなら再生成しない
        if (display.HasHint("PHUD_Role")) return;

        // ── 左カラム（固定 Hint）──────────────────────────────

        // ServerInfo
        display.AddHint(new Hint
        {
            Id        = "PHUD_ServerInfo",
            Alignment = HintAlignment.Center,
            SyncSpeed = HintSyncSpeed.UnSync,
            FontSize  = 18,
            YCoordinate = Y_Server,
            Text = Plugin.Singleton.Config.IsBeta
                ? "[<color=#008cff>Sharp Server</color> - <color=red>BETA</color>]"
                : "[<color=#008cff>Sharp Server</color>]"
        });

        // Event（上部）
        display.AddHint(new Hint
        {
            Id          = "PHUD_Event",
            Alignment   = HintAlignment.Left,
            SyncSpeed   = HintSyncSpeed.Fast,
            FontSize    = 24,
            XCoordinate = LeftX,
            YCoordinate = Y_Event,
            AutoText    = _ => BuildEventText(player)
        });

        // Debug（上部、デバッグ時のみ表示）
        display.AddHint(new Hint
        {
            Id          = "PHUD_Debug",
            Alignment   = HintAlignment.Left,
            SyncSpeed   = HintSyncSpeed.Fast,
            FontSize    = 24,
            XCoordinate = LeftX,
            YCoordinate = Y_Debug,
            AutoText    = ev =>
            {
                ev.NextUpdateDelay = TimeSpan.FromSeconds(0.1);
                if (!DebugModeHandler.IsDebugMode(player))
                    return string.Empty;
                return BuildDebugHud(player);
            }
        });

        // Role
        display.AddHint(new Hint
        {
            Id          = "PHUD_Role",
            Alignment   = HintAlignment.Left,
            SyncSpeed   = HintSyncSpeed.Fast,
            FontSize    = 24,
            XCoordinate = LeftX,
            YCoordinate = Y_Role,
            AutoText    = ev =>
            {
                ev.NextUpdateDelay = TimeSpan.FromSeconds(2);
                var target = ResolveHudTarget(player);
                return "Role: " + BuildRoleText(target);
            }
        });

        // Team
        display.AddHint(new Hint
        {
            Id          = "PHUD_Team",
            Alignment   = HintAlignment.Left,
            SyncSpeed   = HintSyncSpeed.Fast,
            FontSize    = 24,
            XCoordinate = LeftX,
            YCoordinate = Y_Team,
            AutoText    = ev =>
            {
                ev.NextUpdateDelay = TimeSpan.FromSeconds(2);
                var target = ResolveHudTarget(player);
                return "Team: " + BuildTeamText(target);
            }
        });

        // Objective
        display.AddHint(new Hint
        {
            Id          = "PHUD_Objective",
            Alignment   = HintAlignment.Left,
            SyncSpeed   = HintSyncSpeed.Fast,
            FontSize    = 20,
            XCoordinate = LeftX,
            YCoordinate = Y_Objective,
            AutoText    = ev =>
            {
                ev.NextUpdateDelay = TimeSpan.FromSeconds(2);
                var target = ResolveHudTarget(player);
                return "<size=18>Objective: " + BuildObjectiveText(target) + "</size>";
            }
        });

        // ── 右カラム（DynamicHint：自動回避）─────────────────

        // Specific：ロール固有情報（優先度高、上に来る）
        display.AddHint(new DynamicHint
        {
            Id            = "PHUD_Specific",
            SyncSpeed     = HintSyncSpeed.Fast,
            FontSize       = 22,
            TargetX        = RightX,
            TargetY        = Y_Specific_Target,
            TopBoundary    = 800,
            BottomBoundary = 980,
            LeftBoundary   = RightX - 50,
            RightBoundary  = RightX + 600,
            TopMargin      = 4,
            BottomMargin   = 4,
            Priority       = HintPriority.High,
            Strategy       = DynamicHintStrategy.StayInPosition,
            AutoText       = ev =>
            {
                ev.NextUpdateDelay = TimeSpan.FromSeconds(1);
                var target = ResolveHudTarget(player);
                if (!IsValid(target) || !target.IsAlive) return string.Empty;
                return RoleSpecificTextProvider.GetFor(target);
            }
        });

        // Ability：全スロット表示（Specificの下に自動配置）
        display.AddHint(new DynamicHint
        {
            Id             = "PHUD_Ability",
            SyncSpeed      = HintSyncSpeed.Fast,
            FontSize        = 22,
            TargetX         = RightX,
            TargetY         = Y_Ability_Target,
            TopBoundary     = 800,
            BottomBoundary  = 1020,
            LeftBoundary    = RightX - 50,
            RightBoundary   = RightX + 600,
            TopMargin       = 4,
            BottomMargin    = 4,
            Priority        = HintPriority.Medium,
            Strategy        = DynamicHintStrategy.StayInPosition,
            AutoText        = ev =>
            {
                ev.NextUpdateDelay = TimeSpan.FromSeconds(0.5);
                var target = ResolveHudTarget(player);
                return BuildAbilityHud(target);
            }
        });
    }

    // =========================================================
    // HUD テキストビルダー群
    // =========================================================

    private string BuildEventText(Player viewer)
    {
        var target = ResolveHudTarget(viewer);
        // チームが Dead で観戦対象不在なら空
        if (!IsValid(target)) return string.Empty;
        return "[Event]\n<size=26>" +
               SpecialEventsHandler.Instance.LocalizedEventName +
               "</size>";
    }

    private static string BuildRoleText(Player target)
    {
        if (!IsValid(target)) return string.Empty;
        var custom = target.GetCustomRole();
        if (custom != CRoleTypeId.None &&
            RoleHintsDictionary.Table.TryGetValue(custom, out var data))
            return data.Role;
        return GetTeamFallbackRole(target);
    }

    private static string BuildTeamText(Player target)
    {
        if (!IsValid(target)) return string.Empty;
        var custom = target.GetCustomRole();
        if (custom != CRoleTypeId.None &&
            RoleHintsDictionary.Table.TryGetValue(custom, out var data))
            return data.Team;

        // FacilityTermination 特殊処理
        if (SpecialEventsHandler.Instance.NowEvent == SpecialEventType.FacilityTermination)
        {
            var cteam = target.GetTeam();
            if (cteam is CTeam.FoundationForces or CTeam.Guards &&
                target.GetCustomRole() != CRoleTypeId.Sculpture)
                return "<color=#00b7eb>The Foundation</color>";
        }

        return GetTeamFallbackTeam(target);
    }

    private static string BuildObjectiveText(Player target)
    {
        if (!IsValid(target)) return string.Empty;
        var custom = target.GetCustomRole();
        if (custom != CRoleTypeId.None &&
            RoleHintsDictionary.Table.TryGetValue(custom, out var data))
            return data.Objective;

        if (SpecialEventsHandler.Instance.NowEvent == SpecialEventType.FacilityTermination)
        {
            var cteam = target.GetTeam();
            if (cteam is CTeam.FoundationForces or CTeam.Guards &&
                target.GetCustomRole() != CRoleTypeId.Sculpture)
                return "財団に従い、人類を根絶させよ。";
        }

        return GetTeamFallbackObjective(target);
    }

    private static string BuildAbilityHud(Player target)
    {
        if (!IsValid(target) || !target.IsAlive) return string.Empty;
        if (!AbilityManager.TryGetLoadout(target, out var loadout)) return string.Empty;

        bool hasAny = false;
        for (int i = 0; i < AbilityLoadout.MaxSlots; i++)
            if (loadout.Slots[i] != null) { hasAny = true; break; }
        if (!hasAny) return string.Empty;

        var sb = new StringBuilder();
        for (int i = 0; i < AbilityLoadout.MaxSlots; i++)
        {
            var ability = loadout.Slots[i];
            if (ability == null) continue;

            bool isActive   = (i == loadout.ActiveIndex);
            string key      = ability.GetType().Name;
            string dispName = AbilityLocalization.GetDisplayName(key, target);

            string cdText, usesText;
            if (AbilityBase.TryGetAbilityState(target, ability,
                    out bool canUse, out float cdRemain,
                    out int usesLeft, out int maxUses))
            {
                cdText   = canUse ? "<color=green>READY</color>"
                                  : $"<color=yellow>{(int)cdRemain}s</color>";
                usesText = maxUses < 0 ? "∞" : usesLeft.ToString();
            }
            else
            {
                cdText   = "<color=#888888>--</color>";
                usesText = "--";
            }

            sb.AppendLine(isActive
                ? $"<color=#ffcc00>▶[{dispName}]</color> CD:{cdText} Uses:{usesText}"
                : $"<color=#888888>  [{dispName}]</color> CD:{cdText} Uses:{usesText}");
        }

        return sb.ToString().TrimEnd();
    }

    // =========================================================
    // チームフォールバック（static）
    // =========================================================
    private static string GetTeamFallbackRole(Player p)
    {
        string name = p.Role?.Name ?? "";
        return p.Role?.Team switch
        {
            Team.ClassD           => $"<color=#ee7600>{name}</color>",
            Team.Scientists       => $"<color=#faff86>{name}</color>",
            Team.ChaosInsurgency  => $"<color=#228b22>{name}</color>",
            Team.FoundationForces => $"<color=#00b7eb>{name}</color>",
            Team.SCPs             => $"<color=#c50000>{name}</color>",
            Team.Flamingos        => $"<color=#ff96de>{name}</color>",
            _                     => $"<color=#ffffff>{name}</color>",
        };
    }

    private static string GetTeamFallbackTeam(Player p) => p.Role?.Team switch
    {
        Team.ClassD           => "<color=#ee7600>Neutral - Side Chaos</color>",
        Team.Scientists       => "<color=#faff86>Neutral - Side Foundation</color>",
        Team.ChaosInsurgency  => "<color=#228b22>Chaos Insurgency</color>",
        Team.FoundationForces => "<color=#00b7eb>The Foundation</color>",
        Team.SCPs             => "<color=#c50000>The SCPs</color>",
        Team.Flamingos        => "<color=#ff96de>The Flamingos</color>",
        _                     => "<color=#ffffff>[Unknown]</color>",
    };

    private static string GetTeamFallbackObjective(Player p) => p.Role?.Team switch
    {
        Team.ClassD           => "施設から脱出せよ",
        Team.Scientists       => "施設から脱出せよ",
        Team.ChaosInsurgency  => "Dクラス職員を救出し、施設を略奪せよ。",
        Team.FoundationForces => "研究員を救出し、施設の秩序を守護せよ。",
        Team.SCPs             => "己の本能・復讐心と利益の為に動け",
        Team.Flamingos        => "フラミンゴ！",
        _                     => "[Unknown]",
    };

    // =========================================================
    // イベントハンドラ
    // =========================================================
    private void OnVerified(VerifiedEventArgs ev)
    {
        if (!IsValid(ev?.Player)) return;
        SetupHUD(ev.Player);
    }

    private void OnRoundStarted()
    {
        foreach (var player in Player.List.ToList())
        {
            if (!IsValid(player)) continue;
            // HasHint で既存確認済みのため再SetupはHasHint内でガード
            SetupHUD(player);
        }
    }

    private void OnChangingRole(ChangingRoleEventArgs ev)
    {
        if (!IsValid(ev?.Player)) return;
        // ロール変更後に AutoContent が自律更新するので追加処理は不要
        // ただし Dead→生存時にHintが消えていた場合のリカバリ
        Timing.CallDelayed(0.5f, () =>
        {
            if (!IsValid(ev.Player)) return;
            SetupHUD(ev.Player);
        });
    }

    private void OnRestartingRound()
    {
        _spectateTargets.Clear();
        foreach (var player in Player.List.ToList())
        {
            if (!IsValid(player)) continue;
            try { GetDisplay(player)?.ClearHint(); }
            catch (Exception e) { Log.Debug($"[DestroyHints] {player.Nickname}: {e.Message}"); }
        }
    }

    private void OnSpectate(ChangingSpectatedPlayerEventArgs ev)
    {
        if (!IsValid(ev?.Player)) return;
        var spectator = ev.Player;

        if (ev.NewTarget == null)
        {
            _spectateTargets.Remove(spectator.Id);
        }
        else if (IsValid(ev.NewTarget))
        {
            _spectateTargets[spectator.Id] = ev.NewTarget;
        }
        // AutoContent が次の更新サイクルで ResolveHudTarget を呼ぶので
        // 即時反映させたい場合だけ ForceUpdate を叩く
        Timing.CallDelayed(0.1f, () =>
        {
            if (!IsValid(spectator)) return;
            GetDisplay(spectator)?.ForceUpdate(useFastUpdate: true);
        });
    }

    // =========================================================
    // 外部から即時更新したい場合（AbilityManager から呼ぶ用）
    // =========================================================
    public void ForceAbilityHudSync(Player player)
    {
        if (!IsValid(player)) return;
        GetDisplay(player)?.ForceUpdate(useFastUpdate: true);
    }
    
    /// <summary>特定プレイヤーのHintを全消去してHUDを再構築する</summary>
    public void ResetHudForPlayer(Player player)
    {
        if (!IsValid(player)) return;
        try
        {
            GetDisplay(player)?.ClearHint();
        }
        catch (Exception e)
        {
            Log.Debug($"[ResetHudForPlayer] {player.Nickname}: {e.Message}");
        }
    }
    
    /// <summary>全プレイヤーのHUDを即時強制更新する</summary>
    public void ForceUpdateAll()
    {
        foreach (var player in Player.List.ToList())
        {
            if (!IsValid(player)) continue;
            try { GetDisplay(player)?.ForceUpdate(useFastUpdate: true); }
            catch (Exception e) { Log.Debug($"[ForceUpdateAll] {player.Nickname}: {e.Message}"); }
        }
    }

    // =========================================================
    // HintSync（互換性維持、既存呼び出し箇所が残っている場合用）
    // 新規コードではこれを使わず AutoContent に寄せること
    // =========================================================
    public void HintSync(SyncType syncType, string hintText, Player player)
    {
        // AutoContent 移行後は基本的に呼ばれなくなるが、
        // 他箇所が残っている間のフォールバックとして残す
        if (!IsValid(player)) return;
        var display = GetDisplay(player);
        if (display == null) return;

        string id = syncType switch
        {
            SyncType.PHUD_Role      => "PHUD_Role",
            SyncType.PHUD_Team      => "PHUD_Team",
            SyncType.PHUD_Objective => "PHUD_Objective",
            SyncType.PHUD_Event     => "PHUD_Event",
            SyncType.PHUD_Ability   => "PHUD_Ability",
            SyncType.PHUD_Debug     => "PHUD_Debug",
            SyncType.ServerInfo     => "PHUD_ServerInfo",
            _                       => string.Empty
        };
        if (string.IsNullOrEmpty(id)) return;

        if (display.TryGetHint(id, out var hint))
            hint.Text = hintText; // AutoContent を StringContent で上書き（一時的）
    }

    // =========================================================
    // BuildDebugHud（元コードからそのまま移植）
    // =========================================================
    private static string BuildDebugHud(Player player)
    {
        var sb = new StringBuilder();
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
            var invRot   = Quaternion.Inverse(room.Rotation);
            var localPos = invRot * (pos - room.Position);
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
            $"<color=#aaaaaa>Connected Players:</color> {Player.List.Count(p => !p.IsNPC)} " +
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
                string tag     = isCItem ? "<color=#88ffcc>[C]</color>" : "";
                string cur     = isCurrent ? "<color=yellow>▶</color>" : "  ";
                sb.Append($" {cur}{tag}{it.Type}");
            }
            sb.AppendLine();
        }

        // ── 有効なエフェクト一覧 ─────────────────────────────────────
        var activeEffects = player.ActiveEffects.ToList();
        if (activeEffects.Count == 0)
        {
            sb.AppendLine("<color=#666666>Effects: None</color>");
        }
        else
        {
            sb.AppendLine("<color=#aaaaaa>Effects:</color>");
            foreach (var effect in activeEffects)
            {
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
        float elapsed    = RoundHandler.ElapsedTime;
        float waitFor    = RoundHandler.WaitForSpawnTime;
        float remaining  = waitFor - elapsed;

        if (RoundHandler.IsAlreadySpawned)
        {
            sb.AppendLine(
                $"<color=#666666>FirstTeam: {expectedTeam} already spawned.</color>"
            );
        }
        else
        {
            string teamColor = RoundHandler.IsSecurityTeamExpected() ? "#00b7eb" : "#228b22";
            string urgency   = remaining <= 30f ? "<color=#ff4444>" : "<color=#ffcc00>";
            sb.AppendLine(
                $"{urgency}FirstTeam:</color> " +
                $"<color={teamColor}>{expectedTeam}</color> spawns in " +
                $"{urgency}{remaining:F1}s</color> " +
                $"<color=#666666>({elapsed:F1} / {waitFor:F0})</color>"
            );
        }

        sb.Append("</size>");
        return sb.ToString();
    }
}