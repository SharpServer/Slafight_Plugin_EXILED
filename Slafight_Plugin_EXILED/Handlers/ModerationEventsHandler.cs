using System;
using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using Slafight_Plugin_EXILED.API.Core.Extensions;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;

using PlayerHandlers = Exiled.Events.Handlers.Player;
using ServerHandlers = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.Handlers;

/// <summary>
/// 通報・チームキル・Kick/Ban を検知し、<see cref="ModerationBridge"/> 経由で Discord Bot へ通知します。
/// </summary>
/// <remarks>Ban では Kicked も発火するので、Banned 側で記録して Kick 通知を抑制します。</remarks>
public sealed class ModerationEventsHandler : EventHandlerBase
{
    /// <summary>Kicking(pre) で捕捉した実行者を Kicked(post) 発火まで一時保持します。Key: 対象の UserId。</summary>
    private readonly Dictionary<string, Player> pendingKickIssuers = new Dictionary<string, Player>();

    /// <summary>Banned で処理済みの対象。直後に付随して発火する Kicked を二重通知しないための抑制用。Key: 対象の UserId。</summary>
    private readonly HashSet<string> suppressNextKick = [];

    /// <inheritdoc />
    public override void RegisterEvents()
    {
        ServerHandlers.ReportingCheater += OnReportingCheater;
        ServerHandlers.LocalReporting += OnLocalReporting;
        ServerHandlers.RestartingRound += OnRestartingRound;
        PlayerHandlers.Dying += OnDying;
        PlayerHandlers.Kicking += OnKicking;
        PlayerHandlers.Kicked += OnKicked;
        PlayerHandlers.Banned += OnBanned;
    }

    /// <inheritdoc />
    public override void UnregisterEvents()
    {
        ServerHandlers.ReportingCheater -= OnReportingCheater;
        ServerHandlers.LocalReporting -= OnLocalReporting;
        ServerHandlers.RestartingRound -= OnRestartingRound;
        PlayerHandlers.Dying -= OnDying;
        PlayerHandlers.Kicking -= OnKicking;
        PlayerHandlers.Kicked -= OnKicked;
        PlayerHandlers.Banned -= OnBanned;

        pendingKickIssuers.Clear();
        suppressNextKick.Clear();
    }

    private void OnRestartingRound()
    {
        // Kicking/Banned が発火しても対になるイベントが来なかった場合の取りこぼし掃除
        pendingKickIssuers.Clear();
        suppressNextKick.Clear();
    }

    private void OnKicking(KickingEventArgs ev)
    {
        if (ev.Target is null) return;

        pendingKickIssuers[ev.Target.UserId] = ev.Player;
    }

    private void OnKicked(KickedEventArgs ev)
    {
        if (!ev.Player.IsSafePlayer()) return;

        // AFK による自動 Kick はモデレーション操作ではないので通知しない
        if (KickReasonExtensions.TryParseKickReason(ev.Reason, out KickReason reason) && reason is KickReason.AFK)
            return;

        // 直前の Banned 通知に付随する強制切断なので、Kick として二重通知しない
        if (suppressNextKick.Remove(ev.Player.UserId))
        {
            pendingKickIssuers.Remove(ev.Player.UserId);

            return;
        }

        pendingKickIssuers.TryGetValue(ev.Player.UserId, out Player issuer);
        pendingKickIssuers.Remove(ev.Player.UserId);

        (string actorName, string actorId) = FormatActor(issuer);

        ModerationBridge.Send("kick", new
        {
            actor = actorName,
            actorId,
            target = ev.Player.Nickname,
            targetId = ev.Player.UserId,
            reason = ev.Reason,
        });
    }

    private void OnBanned(BannedEventArgs ev)
    {
        if (!ev.Target.IsSafePlayer()) return;

        // この直後に必ず Kicked (強制切断) も発火するため、そちらは無視させる
        suppressNextKick.Add(ev.Target.UserId);

        // ev.Player は Banned イベントでは "実行者" を指す (Target が対象)。
        // Details.Issuer の文字列パースより、EXILED が解決済みの Player を使う方が確実。
        (string actorName, string actorId) = FormatActor(ev.Player);

        ModerationBridge.Send("ban", new
        {
            actor = actorName,
            actorId,
            target = ev.Target.Nickname,
            targetId = ev.Target.UserId,
            duration = FormatDuration(ev.Details),
            reason = ev.Details?.Reason ?? string.Empty,
            banType = ev.Type.ToString(),
            forced = ev.IsForced,
        });
    }

    private static string FormatDuration(BanDetails details)
    {
        if (details is null || details.Expires == DateTime.MaxValue.Ticks)
            return "無期限";

        TimeSpan span = new DateTime(details.Expires, DateTimeKind.Utc) -
                        new DateTime(details.IssuanceTime, DateTimeKind.Utc);

        return span.TotalDays >= 1
            ? $"{span.TotalDays:0.#}日"
            : $"{span.TotalHours:0.#}時間";
    }

    private static (string Name, string Id) FormatActor(Player issuer)
    {
        if (issuer is null || issuer.IsHost)
            return ("サーバーコンソール", null);

        return (issuer.Nickname, issuer.UserId);
    }

    private void OnReportingCheater(ReportingCheaterEventArgs ev)
    {
        if (!ev.Player.IsSafePlayer() || !ev.Target.IsSafePlayer()) return;

        ModerationBridge.Send("report_cheater", new
        {
            reporter = ev.Player.Nickname,
            reporterId = ev.Player.UserId,
            target = ev.Target.Nickname,
            targetId = ev.Target.UserId,
            reason = ev.Reason,
        });
    }

    private void OnLocalReporting(LocalReportingEventArgs ev)
    {
        if (!ev.Player.IsSafePlayer() || !ev.Target.IsSafePlayer()) return;

        ModerationBridge.Send("report_local", new
        {
            reporter = ev.Player.Nickname,
            reporterId = ev.Player.UserId,
            target = ev.Target.Nickname,
            targetId = ev.Target.UserId,
            reason = ev.Reason,
        });
    }

    /// <remarks>味方判定は <see cref="CustomTeam.AreAllies"/> に任せます。陣営を持たない役職は対象外です。</remarks>
    private void OnDying(DyingEventArgs ev)
    {
        Player attacker = ev.Attacker;
        Player victim = ev.Player;

        if (!attacker.IsSafePlayer() || !victim.IsSafePlayer() || attacker == victim) return;
        if (attacker.Role.Side is Side.None || victim.Role.Side is Side.None) return;
        if (!CustomTeam.AreAllies(attacker, victim)) return;

        ModerationBridge.Send("friendly_fire", new
        {
            attacker = attacker.Nickname,
            attackerId = attacker.UserId,
            victim = victim.Nickname,
            victimId = victim.UserId,
            team = attacker.GetTeam()?.Name ?? attacker.Role.Side.ToString(),
            damageType = ev.DamageHandler?.Type.ToString() ?? "Unknown",
        });
    }
}
