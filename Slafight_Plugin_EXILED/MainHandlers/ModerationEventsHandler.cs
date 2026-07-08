using System;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.MainHandlers;

/// <summary>
/// 通報(F7チーター報告 / ローカル通報)、チームキル(FF)、Kick/Ban(RAコンソール発行分も含む)を検知し、
/// <see cref="ModerationBridge"/> 経由で Discord Bot 側へ通知する。
/// RAの "ban" コマンドは Kick(duration=0)/Ban(duration&gt;0) 双方とも内部的に BanPlayer.BanUser を経由するため、
/// Exiled の Kicked/Banned イベントをフックすれば ModeratorUtil 経由・RAコンソール経由の両方を捕捉できる
/// (デコンパイルで確認済み)。ただし Kicked イベントには実行者情報が乗らないため target/reason のみ通知する。
/// Warn は既存フレームワークにイベントが無いため、実行者が確定している ModeratorUtil 側から直接通知する。
/// </summary>
public static class ModerationEventsHandler
{
    public static void Register()
    {
        Exiled.Events.Handlers.Server.ReportingCheater += OnReportingCheater;
        Exiled.Events.Handlers.Server.LocalReporting += OnLocalReporting;
        Exiled.Events.Handlers.Player.Dying += OnDying;
        Exiled.Events.Handlers.Player.Kicked += OnKicked;
        Exiled.Events.Handlers.Player.Banned += OnBanned;
    }

    public static void Unregister()
    {
        Exiled.Events.Handlers.Server.ReportingCheater -= OnReportingCheater;
        Exiled.Events.Handlers.Server.LocalReporting -= OnLocalReporting;
        Exiled.Events.Handlers.Player.Dying -= OnDying;
        Exiled.Events.Handlers.Player.Kicked -= OnKicked;
        Exiled.Events.Handlers.Player.Banned -= OnBanned;
    }

    private static void OnKicked(KickedEventArgs ev)
    {
        if (!ev.Player.IsSafePlayer()) return;

        ModerationBridge.Send("kick", new
        {
            target = ev.Player.Nickname,
            targetId = ev.Player.UserId,
            reason = ev.Reason,
        });
    }

    private static void OnBanned(BannedEventArgs ev)
    {
        if (!ev.Target.IsSafePlayer()) return;

        var details = ev.Details;
        string issuer = details?.Issuer;
        string reason = details?.Reason ?? string.Empty;
        string durationLabel = "無期限";

        if (details != null && details.Expires != DateTime.MaxValue.Ticks)
        {
            var issuedAt = new DateTime(details.IssuanceTime, DateTimeKind.Utc);
            var expiresAt = new DateTime(details.Expires, DateTimeKind.Utc);
            var span = expiresAt - issuedAt;
            durationLabel = span.TotalDays >= 1
                ? $"{span.TotalDays:0.#}日"
                : $"{span.TotalHours:0.#}時間";
        }

        ModerationBridge.Send("ban", new
        {
            actor = issuer,
            target = ev.Target.Nickname,
            targetId = ev.Target.UserId,
            duration = durationLabel,
            reason,
            banType = ev.Type.ToString(),
            forced = ev.IsForced,
        });
    }

    private static void OnReportingCheater(ReportingCheaterEventArgs ev)
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

    private static void OnLocalReporting(LocalReportingEventArgs ev)
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

    private static void OnDying(DyingEventArgs ev)
    {
        var attacker = ev.Attacker;
        var victim = ev.Player;
        if (!attacker.IsSafePlayer() || !victim.IsSafePlayer() || attacker == victim) return;

        var attackerTeam = attacker.GetTeam();
        var victimTeam = victim.GetTeam();
        if (attackerTeam != victimTeam) return;
        if (attackerTeam is CTeam.Others or CTeam.Null) return;

        ModerationBridge.Send("friendly_fire", new
        {
            attacker = attacker.Nickname,
            attackerId = attacker.UserId,
            victim = victim.Nickname,
            victimId = victim.UserId,
            team = attackerTeam.ToString(),
            damageType = ev.DamageHandler?.Type.ToString() ?? "Unknown",
        });
    }
}
