#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Models.Hints;
using HintServiceMeow.Core.Utilities;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Hint = HintServiceMeow.Core.Models.Hints.Hint;
using HintParameter = Hints.HintParameter;
using SSKeybindHintParameter = Hints.SSKeybindHintParameter;

namespace Slafight_Plugin_EXILED.Hints;

public class PlayerHUD : EventHandlerBase
{
    /// <summary>
    /// ゲームモードが走っていないときに [Event] 欄へ出す文字列です。
    /// </summary>
    /// <remarks>移行前の <c>SpecialEventsHandler.LocalizedEventName</c> の既定値と同じです。</remarks>
    private const string NoGameModeText = "無し";

    /// <summary>部隊表示で並べる行数の上限です。</summary>
    /// <remarks>
    /// <c>ForceHud.Rows</c> が返しうる最大行数より多めに取ってあります。
    /// 足りないと下の行が黙って消えるので、増やすときはこちらも上げてください。
    /// </remarks>
    private const int ForceHudMaxRows = 18;

    /// <summary>部隊表示の文字の大きさです。</summary>
    private const int ForceHudFontSize = 23;

    /// <summary>部隊表示の横位置です。0 が中央、負で左、正で右。</summary>
    /// <remarks>
    /// <c>Alignment.Left</c> と組で使います。<c>Center</c> だと HSM が
    /// <c>&lt;align&gt;</c> を書かず、TMP の中央揃えが <c>&lt;pos&gt;</c> に勝つため位置が効きません。
    /// 画面右上はバニラの <c>UnitNamingHud</c> が使うので、そこは避けています。
    /// </remarks>
    private const int ForceHudX = 0;

    /// <summary>部隊表示の 1 行目の縦位置です。0 が画面上端。</summary>
    private const int ForceHudTopY = 60;

    /// <summary>デバッグ表示の横位置です。通常 HUD の左列に揃えます。</summary>
    private const int DebugHudX = -350;

    /// <summary>デバッグ表示の縦位置です。</summary>
    private const int DebugHudY = 345;

    /// <summary>デバッグ表示の文字の大きさです。</summary>
    private const int DebugHudFontSize = 24;

    /// <summary>部隊表示の行送りです。</summary>
    /// <remarks>
    /// 各行は独立した Hint なので、HSM は行送りを自動計算しません。
    /// <see cref="ForceHudFontSize"/> より少し大きい値にして行が重ならないようにします。
    /// </remarks>
    private const int ForceHudLineHeight = 26;

    /// <summary>
    /// 現在動いている HUD です。表示を差し込む側 (<c>EffectedInfoTextProvider</c> など) が参照します。
    /// </summary>
    /// <remarks>
    /// 生成は <c>EventHandlerRegistry</c> が行います。ここは購読が始まった実体を指すだけで、
    /// <c>Lifetime</c> を読むために作られて捨てられる実体は入りません。
    /// </remarks>
    public static PlayerHUD? Instance { get; private set; }

    private CoroutineHandle _specificAbilityLoop;
    private CoroutineHandle _forceHudLoop;
    private CoroutineHandle _bulkSetupHandle;

    /// <summary>全員分の HUD 構築を1フレームに何ミリ秒まで詰め込むか。</summary>
    private const double HudSetupFrameBudgetMs = 4d;
    private CoroutineHandle _abilityHudLoop;
    private CoroutineHandle _taskSyncLoop;
    private CoroutineHandle _debugHudLoop;

    // 観戦者ID → 現在見ているプレイヤー
    private readonly Dictionary<int, Player> _spectateTargets = new();

    /// <inheritdoc />
    public override void RegisterEvents()
    {
        Instance = this;

        Exiled.Events.Handlers.Player.Verified += ServerInfoHint;
        // PlayerHUDMain が HUD 構築とロール情報の反映を両方やるため、
        // 同じ RoundStarted で AllSyncHUD_ を重ねて呼ぶ必要はない（全員分の二度手間だった）。
        Exiled.Events.Handlers.Server.RoundStarted += PlayerHUDMain;
        Exiled.Events.Handlers.Player.ChangingRole += AllSyncHUD;
        Exiled.Events.Handlers.Server.RestartingRound += DestroyHints;
        Exiled.Events.Handlers.Player.ChangingSpectatedPlayer += Spectate;
        Exiled.Events.Handlers.Player.Left += OnLeft;

        // 旧仕様と同じく、コルーチンはプラグイン生存中ずっと回す
        _specificAbilityLoop = Timing.RunCoroutine(SpecificInfoHudLoop());
        _forceHudLoop = Timing.RunCoroutine(ForceHudLoop());
        _abilityHudLoop = Timing.RunCoroutine(AbilityHudLoop());
        _taskSyncLoop = Timing.RunCoroutine(TaskSync());
        _debugHudLoop = Timing.RunCoroutine(DebugHudLoop());
    }

    /// <inheritdoc />
    public override void UnregisterEvents()
    {
        if (ReferenceEquals(Instance, this))
            Instance = null;

        Exiled.Events.Handlers.Player.Verified -= ServerInfoHint;
        Exiled.Events.Handlers.Server.RoundStarted -= PlayerHUDMain;
        Exiled.Events.Handlers.Player.ChangingRole -= AllSyncHUD;
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

        if (_forceHudLoop.IsRunning)
            Timing.KillCoroutines(_forceHudLoop);

        if (_bulkSetupHandle.IsRunning)
            Timing.KillCoroutines(_bulkSetupHandle);

        _spectateTargets.Clear();
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

    /// <summary>
    /// 内容が変わったときだけ Text を書き、変化があったかを返す。
    /// HintServiceMeow の <c>AbstractHint.Text</c> setter は同じ文字列を代入しても必ず更新イベントを出し、
    /// SyncSpeed が Fastest ならその場でディスプレイ全体の再パースが走る。
    /// 毎秒回る HUD ループから無条件に書くとこれが常時発火するため、必ずこの経路を使うこと。
    /// </summary>
    private static bool SetTextIfChanged(AbstractHint? hint, string? text)
    {
        if (hint == null)
            return false;

        if (string.Equals(hint.Text, text, StringComparison.Ordinal))
            return false;

        hint.Text = text;
        return true;
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
        if (Round.IsLobby) return; // Waiting中(Tutorial含む)はRoundStartまで通常HUD群を作らない

        var display = TryGetDisplay(player);
        if (display == null) return;

        int XCordinate = -350;

        EnsureServerInfoHint(display);
        // SyncSpeed は「ヒント1つの更新でディスプレイ全体を何秒以内に再パースするか」。
        // Fastest は待ち時間 0 = 即時フルパースなので、押した瞬間の反応が要るものだけに絞る。
        // ロール/陣営/目標はロール変更時にしか変わらないため Normal (0.3s) で十分。
        EnsurePlayerHudHint(display, HudConstId.PlayerHUD_Role, "Role: " + player.CustomInfo, HintAlignment.Left, HintSyncSpeed.Normal, 23, XCordinate, 860);
        EnsurePlayerHudHint(display, HudConstId.PlayerHUD_Objective, "Objective: Undefined", HintAlignment.Left, HintSyncSpeed.Normal, 25, XCordinate, 915);
        EnsurePlayerHudHint(display, HudConstId.PlayerHUD_Team, "Team: Undefined", HintAlignment.Left, HintSyncSpeed.Normal, 23, XCordinate, 885);
        EnsurePlayerHudHint(display, HudConstId.PlayerHUD_Event, "[Event]\n<size=28>Undefined</size>", HintAlignment.Left, HintSyncSpeed.Fast, 26, XCordinate, 120);
        EnsurePlayerHudHint(display, HudConstId.PlayerHUD_Specific, string.Empty, HintAlignment.Left, HintSyncSpeed.Fast, 23, XCordinate + 350, 880);
        EnsurePlayerHudHint(display, HudConstId.PlayerHUD_Ability, string.Empty, HintAlignment.Left, HintSyncSpeed.Fast, 22, XCordinate + 350, 800);
        EnsurePlayerHudHint(display, HudConstId.PlayerHUD_EffectedInfo, string.Empty, HintAlignment.Center, HintSyncSpeed.Fast, 22, 0, 930);
        EnsurePlayerHudHint(display, HudConstId.PlayerHUD_Debug, string.Empty, HintAlignment.Left, HintSyncSpeed.Fast, DebugHudFontSize, DebugHudX, DebugHudY);
    }

    private static string BuildServerInfoText()
    {
        string versionInfoString = PluginInfoUtils.GetVersionString();
        return Plugin.Singleton.Config.IsBeta
            ? $"[<color=#008cff>Sharp Server</color> - <color=red>BETA</color> | {versionInfoString}]"
            : $"[<color=#008cff>Sharp Server</color> | {versionInfoString}]";
    }

    private static AbstractHint EnsureServerInfoHint(PlayerDisplay display)
    {
        var existing = display.GetHint(HudConstId.PlayerHUD_ServerInfo);
        if (existing != null)
        {
            SetTextIfChanged(existing, BuildServerInfoText());
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

        // レイアウト系の setter も Text と同じく無条件で更新イベントを出す。
        // ForceHud ループ（毎秒 人数 x 18行）やラウンド開始でまとめて走るため、変わった項目だけ書く。
        if (hint.Alignment != alignment)
            hint.Alignment = alignment;

        if (hint.SyncSpeed != syncSpeed)
            hint.SyncSpeed = syncSpeed;

        if (hint.FontSize != fontSize)
            hint.FontSize = fontSize;

        if (hint.XCoordinate != x)
            hint.XCoordinate = x;

        if (hint.YCoordinate != y)
            hint.YCoordinate = y;

        if (!hint.ResolutionBasedAlign)
            hint.ResolutionBasedAlign = true;
    }

    public void PlayerHUDMain()
    {
        // 旧仕様寄り：RoundStarted 時点で全員分 HUD 作成。
        // ただし1フレームに集中させない（BulkSetupCoroutine を参照）。
        StartBulkSetup();
    }

    /// <summary>
    /// 全員分の HUD 構築をフレーム予算で分割して走らせる。
    ///
    /// HSM はヒントを1つ追加・変更するたびにディスプレイ全体の再パースを予約するため、
    /// 「人数 x ヒント生成 + ロール情報の構築」をラウンド開始の同じフレームで一気にやると
    /// そこで目に見える詰まりになる。ラウンド終了時の HUD 作り直しも同じ形。
    /// </summary>
    private void StartBulkSetup()
    {
        if (_bulkSetupHandle.IsRunning)
            Timing.KillCoroutines(_bulkSetupHandle);

        _bulkSetupHandle = Timing.RunCoroutine(BulkSetupCoroutine());
    }

    private IEnumerator<float> BulkSetupCoroutine()
    {
        // フレームをまたぐのでスナップショットを取る。
        var players = Player.List.ToList();
        var stopwatch = Stopwatch.StartNew();

        foreach (Player player in players)
        {
            if (!IsPlayerValid(player)) continue;

            var display = TryGetDisplay(player);
            if (display == null) continue;

            EnsureServerInfoHint(display);

            if (!Round.IsLobby)
            {
                // 旧 PlayerHUDMain と同じく観戦者も含めて反映する。
                // 観戦中の表示は ChangingSpectatedPlayer 側で対象の情報に差し替わる。
                PlayerHUDSetup(player);
                ApplyRoleInfo(player, player);
            }

            if (stopwatch.Elapsed.TotalMilliseconds < HudSetupFrameBudgetMs)
                continue;

            yield return Timing.WaitForOneFrame;
            stopwatch.Restart();
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
                    SetTextIfChanged(EnsureServerInfoHint(display), hintText);
                    break;
                case SyncType.PHUD_Role:
                    var role = display.GetHint(HudConstId.PlayerHUD_Role);
                    SetTextIfChanged(role, "Role: " + hintText);
                    break;
                case SyncType.PHUD_Objective:
                    var obj = display.GetHint(HudConstId.PlayerHUD_Objective);
                    SetTextIfChanged(obj, "Objective: " + hintText);
                    break;
                case SyncType.PHUD_Team:
                    var team = display.GetHint(HudConstId.PlayerHUD_Team);
                    SetTextIfChanged(team, "Team: " + hintText);
                    break;
                case SyncType.PHUD_Event:
                    var ev = display.GetHint(HudConstId.PlayerHUD_Event);
                    SetTextIfChanged(ev, "[Event]\n<size=28>" + hintText + "</size>");
                    break;
                case SyncType.PHUD_Specific:
                    var specific = display.GetHint(HudConstId.PlayerHUD_Specific);
                    SetTextIfChanged(specific, hintText);
                    break;
                case SyncType.PHUD_Ability:
                    var ab = display.GetHint(HudConstId.PlayerHUD_Ability);
                    SetTextIfChanged(ab, hintText);
                    break;
                case SyncType.PHUD_EffectedInfo:
                    var effected = display.GetHint(HudConstId.PlayerHUD_EffectedInfo);
                    SetTextIfChanged(effected, hintText);
                    break;
                case SyncType.PHUD_Debug:
                    var db = display.GetHint(HudConstId.PlayerHUD_Debug);
                    SetTextIfChanged(db, hintText);
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

            // 役職が自分で名乗るものをそのまま読む。表示層は表を引かない。
            if (CustomRole.Of(sourcePlayer) is { } customRole)
            {
                roleText      = customRole.HudLabel;
                teamText      = customRole.TeamLabel ?? customRole.Team?.HudName ?? string.Empty;
                objectiveText = customRole.Objective ?? customRole.Team?.Objective ?? string.Empty;
            }
            else
            {
                (roleText, teamText, objectiveText) = GetTeamFallback(sourcePlayer);
            }

            HintSync(SyncType.PHUD_Role,      roleText,      targetForHint);
            HintSync(SyncType.PHUD_Objective, objectiveText, targetForHint);
            HintSync(SyncType.PHUD_Team,      teamText,      targetForHint);
            HintSync(SyncType.PHUD_Event,     GameMode.Current?.Name ?? NoGameModeText, targetForHint);
        }
        catch (Exception e)
        {
            Log.Debug($"[ApplyRoleInfo] Exception for {sourcePlayer?.Nickname}: {e.Message}");
        }
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
            foreach (Player player in Player.List)
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
            if (Round.IsLobby) return; // RoundStartまでは通常HUD群を同期しない
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

        ApplyAbilityHud(abilityHint, player);
    }

    public bool ForceDebugHudSync(Player player, bool logException = false)
    {
        if (!IsPlayerValid(player)) return false;

        var display = TryGetDisplay(player);
        if (display == null) return false;

        var debugHint = display.GetHint(HudConstId.PlayerHUD_Debug);
        if (debugHint == null)
        {
            // DebugModeはロビー中でも見たいので、PlayerHUDSetup(通常HUD群)は経由せず単独で作る
            EnsurePlayerHudHint(display, HudConstId.PlayerHUD_Debug, string.Empty, HintAlignment.Left, HintSyncSpeed.Fast, DebugHudFontSize, DebugHudX, DebugHudY);
            debugHint = display.GetHint(HudConstId.PlayerHUD_Debug);
            if (debugHint == null) return false;
        }

        try
        {
            debugHint.Text = BuildDebugHud(player);
            return true;
        }
        catch (Exception e)
        {
            if (logException)
                Log.Debug($"[ForceDebugHudSync] Exception for {player.Nickname}: {e.Message}");

            return false;
        }
    }

    /// <summary>
    /// デバッグ表示を消します。
    /// </summary>
    /// <remarks>
    /// <b>OFF にしたら必ずここを通します。</b>デバッグループは ON の人しか触らないので、
    /// 消さないと最後に描いた内容が画面に残り続けます。
    /// ヒント自体は作り直さず、中身だけ空にします。
    /// </remarks>
    /// <returns>消すべきものがあって消したなら true。</returns>
    public bool ClearDebugHud(Player player)
    {
        if (!IsPlayerValid(player)) return false;

        var display = TryGetDisplay(player);

        // まだヒントが無いなら消すものも無い。ここで作らない。
        if (display?.GetHint(HudConstId.PlayerHUD_Debug) is not { } debugHint) return false;

        if (string.IsNullOrEmpty(debugHint.Text)) return false;

        debugHint.Text = string.Empty;

        return true;
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
                SetTextIfChanged(specificHint, RoleSpecificTextProvider.GetFor(target));
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
                ApplyAbilityHud(abilityHint, target);
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
        foreach (Player player in Player.List)
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

        // 作り直しも1フレームに詰め込まない。
        Timing.CallDelayed(RoleSpawnTimings.HudRecreateAfterClear, StartBulkSetup);

        // ★ コルーチンは止めない（旧仕様の安定性維持）
    }

    // =========================================================
    // Ability HUD
    // =========================================================

    private static void ApplyAbilityHud(
        AbstractHint abilityHint,
        Player target)
    {
        var content = BuildAbilityHud(target);

        // Parameters は毎回新しい配列・新しい SSKeybindHintParameter になる。
        // HSM の送信重複判定 (PlayerDisplay.SameParameterReferences) は要素の参照比較なので、
        // 無条件に代入すると内容が同じでも判定が必ず外れ、0.5 秒ごとに HUD 全文が実送信される。
        // 本文が同じなら、パラメータの並びも同じ（本文が {0} {1} の位置を持っているため）なので参照ごと据え置く。
        if (!SetTextIfChanged(abilityHint, content.Text))
            return;

        abilityHint.Parameters = content.Parameters;
    }

    /// <summary>
    /// 能力欄の中身です。
    /// </summary>
    /// <remarks>
    /// 表示の書式は移行前のままです。読む先だけが
    /// 旧 <c>AbilityManager</c> / <c>AbilityLoadout</c> / <c>AbilityLocalization</c> から
    /// <see cref="AbilityBase"/> に変わりました。枠順は付与順そのもので、
    /// 呼び名は配った側が <c>Rename</c> したものが出ます。
    /// </remarks>
    private static ServerSpecificUserSettings.KeybindHintContent BuildAbilityHud(Player target)
    {
        if (!IsPlayerValid(target))
            return new ServerSpecificUserSettings.KeybindHintContent(string.Empty, []);

        if (!target.IsAlive)
            return new ServerSpecificUserSettings.KeybindHintContent(string.Empty, []);

        var entries = AbilityBase.Of(target);
        if (entries.Count == 0)
            return new ServerSpecificUserSettings.KeybindHintContent(string.Empty, []);

        var activeEntryIndex = AbilityBase.ActiveIndexOf(target);
        var active = entries[activeEntryIndex];
        var abilityName = active.DisplayName;
        var optionName = active.SelectedOption?.Name;
        if (!string.IsNullOrWhiteSpace(optionName))
            abilityName += $" <color=#8fdcff>[{optionName}]</color>";

        var statusText = FormatAbilityState(active, out var usesText);
        var countText = $"{activeEntryIndex + 1}/{entries.Count}";
        var parameters = new List<HintParameter>
        {
            new SSKeybindHintParameter(ServerSpecifics.AbilityUseKeybindSettingId)
        };
        var controlParts = new List<string> { "<color=#aaffaa>{0}</color>:使用" };
        var parameterIndex = 1;

        if (entries.Count > 1)
        {
            parameters.Add(new SSKeybindHintParameter(ServerSpecifics.AbilitySwitchKeybindSettingId));
            controlParts.Add($"<color=#aaffaa>{{{parameterIndex++}}}</color>:切替");
        }
        else
        {
            controlParts.Add("所持:1");
        }

        if (active.HasSelectableOptions)
        {
            parameters.Add(new SSKeybindHintParameter(ServerSpecifics.AbilityOptionPreviousKeybindSettingId));
            parameters.Add(new SSKeybindHintParameter(ServerSpecifics.AbilityOptionNextKeybindSettingId));
            controlParts.Add($"<color=#aaffaa>{{{parameterIndex++}}}</color>/<color=#aaffaa>{{{parameterIndex++}}}</color>:オプション");
        }

        var controlText = string.Join(" / ", controlParts);

        var slotSummary = BuildAbilitySlotSummary(entries, activeEntryIndex);

        return new ServerSpecificUserSettings.KeybindHintContent(
            $"<size=22><color=#ffcc00>Ability {countText}</color> {abilityName} {statusText} Uses:{usesText}</size>\n" +
            $"<size=18>{controlText} | {slotSummary}</size>",
            parameters.ToArray());
    }

    private static string BuildAbilitySlotSummary(
        IReadOnlyList<AbilityBase> entries,
        int activeEntryIndex)
    {
        var sb = new StringBuilder();

        for (var i = 0; i < entries.Count; i++)
        {
            if (i > 0)
                sb.Append(" | ");

            var ability = entries[i];
            var marker = i == activeEntryIndex
                ? $"<color=#ffcc00>*{i + 1}</color>"
                : (i + 1).ToString();
            var name = ShortenAbilityName(ability.DisplayName, 8);

            sb.Append(marker)
                .Append(':')
                .Append(name)
                .Append(' ')
                .Append(FormatCompactAbilityState(ability));
        }

        return sb.ToString();
    }

    private static string FormatAbilityState(AbilityBase ability, out string usesText)
    {
        usesText = ability.MaxUses < 0 ? "∞" : Math.Max(0, ability.RemainingUses).ToString();

        if (ability.MaxUses >= 0 && ability.RemainingUses <= 0)
            return "<color=#ff6666>DONE</color>";

        return ability.IsReady
            ? "<color=#38ff6b>READY</color>"
            : $"<color=#ffd966>CD {Mathf.CeilToInt(ability.RemainingCooldown)}s</color>";
    }

    private static string FormatCompactAbilityState(AbilityBase ability)
    {
        if (ability.MaxUses >= 0 && ability.RemainingUses <= 0)
            return "<color=#ff6666>0</color>";

        return ability.IsReady
            ? "<color=#38ff6b>OK</color>"
            : $"<color=#ffd966>{Mathf.CeilToInt(ability.RemainingCooldown)}s</color>";
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

            foreach (var player in Player.List)
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
                    ApplyAbilityHud(abilityHint, hudTarget);
                }
                catch (Exception e)
                {
                    Log.Debug($"[AbilityHudLoop] Exception for {player.Nickname}: {e.Message}");
                }
            }

            yield return Timing.WaitForSeconds(0.5f);
        }
    }

    /// <summary>
    /// 右上の部隊一覧を 1 秒ごとに描き直すループ。
    /// </summary>
    /// <remarks>
    /// <b>1 行 = 1 Hint</b> に分けています。<c>HintAlignment.Right</c> は
    /// HSM 側で <c>&lt;margin-right&gt;</c> とアスペクト比補正を通るため解像度で位置が動きます。
    /// Center + <c>XCoordinate</c> なら <c>&lt;pos&gt;</c> だけで済み、どの解像度でも同じ列に揃います。
    ///
    /// 使わなくなった行は空文字にせず <c>RemoveHint</c> で消します。
    /// 空の Hint を残すと、隊が減ったときに詰まらず穴が空いて見えます。
    /// </remarks>
    private IEnumerator<float> ForceHudLoop()
    {
        yield return Timing.WaitForSeconds(1f);

        for (;;)
        {
            if (Round.IsLobby)
            {
                yield return Timing.WaitForSeconds(1f);
                continue;
            }

            foreach (var player in Player.List)
            {
                if (!IsPlayerValid(player)) continue;

                // 観戦中は見ている相手の部隊を出す。他のループと同じ扱い。
                var hudTarget = player;
                if (player.Role?.Team == Team.Dead &&
                    _spectateTargets.TryGetValue(player.Id, out var spectated) &&
                    IsPlayerValid(spectated) && spectated.IsAlive)
                    hudTarget = spectated;

                var display = TryGetDisplay(player);
                if (display == null) continue;

                try
                {
                    ApplyForceRows(display, ForceHud.Rows(hudTarget));
                }
                catch (Exception e)
                {
                    Log.Debug($"[ForceHudLoop] Exception for {player.Nickname}: {e.Message}");
                }
            }

            yield return Timing.WaitForSeconds(1f);
        }
    }

    /// <summary>
    /// 行の一覧を、行ごとの Hint に反映します。
    /// </summary>
    private static void ApplyForceRows(PlayerDisplay display, IReadOnlyList<string> rows)
    {
        for (int index = 0; index < ForceHudMaxRows; index++)
        {
            string id = ForceRowId(index);

            if (index >= rows.Count)
            {
                if (display.GetHint(id) is { } stale)
                    display.RemoveHint(stale);

                continue;
            }

            EnsurePlayerHudHint(
                display,
                id,
                string.Empty,
                HintAlignment.Right,
                HintSyncSpeed.Fast,
                ForceHudFontSize,
                ForceHudX,
                ForceHudTopY + (index * ForceHudLineHeight));

            if (display.GetHint(id) is { } hint)
                SetTextIfChanged(hint, rows[index]);
        }
    }

    /// <summary>行ごとの Hint の Id です。</summary>
    private static string ForceRowId(int index) => $"{HudConstId.PlayerHUD_Forces}_{index}";

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

            foreach (var player in Player.List)
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

                    SetTextIfChanged(
                        specificHint,
                        string.IsNullOrEmpty(roleSpecific) ? string.Empty : roleSpecific);
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
    /// 0.1 秒ごとに PHUD_Debug ヒントを <see cref="DebugMode"/> の状態へ合わせるループ。
    /// </summary>
    /// <remarks>
    /// <b>ON 側だけでなく OFF 側も毎回書きます。</b>切り替え口はコマンドと
    /// Server Specifics の 2 つあるので、切り替えた側から描画を叩く作りにすると
    /// どちらかを足したときに消し忘れます。ここが状態を見て描き分ける唯一の場所です。
    /// </remarks>
    private IEnumerator<float> DebugHudLoop()
    {
        yield return Timing.WaitForSeconds(0.5f);
 
        for (;;)
        {
            foreach (var player in Player.List)
            {
                if (!IsPlayerValid(player)) continue;

                if (DebugMode.IsEnabled(player))
                    ForceDebugHudSync(player);
                else
                    ClearDebugHud(player);
            }
 
            yield return Timing.WaitForSeconds(0.1f);
        }
    }
    
    private static string BuildDebugHud(Player player)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<size=18><color=#ffff00>[DEBUG MODE]</color>");

        // ── ロール・チーム情報 ────────────────────────────────────────
        sb.AppendLine(
            $"<color=#aaaaaa>Role:</color> {player.Role?.Name ?? "None"}  " +
            $"<color=#aaaaaa>Team:</color> {player.Role?.Team.ToString() ?? "None"}  " +
            $"<color=#aaaaaa>CRole:</color> {CustomRole.Of(player)?.Name ?? "None"}  " +
            $"<color=#aaaaaa>CTeam:</color> {CustomTeam.Of(player)?.Name ?? "None"}"
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
            $"<color=#aaaaaa>Connected Players:</color> {Player.List.Count(p => !p.IsNPC && p.IsSafePlayer())} " +
            $"<color=#aaaaaa>Npcs:</color> {Npc.List.Count} "
        );

        // ── 核弾頭タイマー情報 ───────────────────────────────────────
        if (Warhead.IsInProgress)
        {
            sb.AppendLine(
                $"<color=#ff4444>Warhead:</color> " +
                $"DetonationTimer={Warhead.DetonationTimer:F1}  " +
                $"RealTimer={Warhead.RealDetonationTimer:F1}  " +
                $"IsLocked={Bool(Warhead.IsLocked)}"
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

            // カスタムアイテム情報
            if (CustomItem.Is(currentItem.Serial, out CustomItem custom))
            {
                sb.AppendLine(
                    $"<color=#88ffcc>[CustomItem]</color> " +
                    $"<color=#aaaaaa>Type:</color> {custom.GetType().Name}  " +
                    $"<color=#aaaaaa>Display:</color> {custom.Name}");

                if (custom is CustomHybrid hybrid)
                    sb.Append(hybrid.GetDebugStateFor(player, currentItem.Serial));
            }

            // ── Firearm 情報 ───────────────────────────────────────────
            if (currentItem is Firearm firearm)
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
                var recoil = firearm.Recoil;
                sb.AppendLine(
                    $"  <color=#aaaaaa>Recoil:</color> Time={recoil.AnimationTime:F3}  " +
                    $"Z={recoil.ZAxis:F2}  " +
                    $"Fov={recoil.FovKick:F2}  " +
                    $"Up={recoil.UpKick:F2}  " +
                    $"Side={recoil.SideKick:F2}"
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
            if (currentItem is Armor armor)
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
                bool isCItem   = CustomItem.Is(it.Serial, out _);
                string tag     = isCItem ? "<color=#88ffcc>[C]</color>" : "";
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

        // ────────────────────────────────────────────────────────────
        // ★ 新しい項目はここに追加するだけでOK
        // 例:
        // sb.AppendLine($"<color=#aaaaaa>HP:</color> {player.Health:F0}/{player.MaxHealth:F0}");
        // ────────────────────────────────────────────────────────────

        sb.Append("</size>");
        return sb.ToString();
}
}
