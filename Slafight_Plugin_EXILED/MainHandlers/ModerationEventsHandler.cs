using System;
using System.Collections.Generic;
using Exiled.API.Features;
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
/// (デコンパイルで確認済み)。ただし post イベントの Kicked には実行者情報が乗らないため、
/// pre イベントの Kicking (実行者を含む) で一時的に対応付けてから Kicked 発火時に合成する。
/// Warn は既存フレームワークにイベントが無いため、実行者が確定している ModeratorUtil 側から直接通知する。
/// </summary>
public static class ModerationEventsHandler
{
    // Kicking(pre) で捕捉した実行者を Kicked(post) 発火まで一時保持する。Key: 対象のUserId
    private static readonly Dictionary<string, Player> PendingKickIssuers = new();

    public static void Register()
    {
        Exiled.Events.Handlers.Server.ReportingCheater += OnReportingCheater;
        Exiled.Events.Handlers.Server.LocalReporting += OnLocalReporting;
        Exiled.Events.Handlers.Server.RestartingRound += OnRestartingRound;
        Exiled.Events.Handlers.Player.Dying += OnDying;
        Exiled.Events.Handlers.Player.Kicking += OnKicking;
        Exiled.Events.Handlers.Player.Kicked += OnKicked;
        Exiled.Events.Handlers.Player.Banned += OnBanned;
    }

    public static void Unregister()
    {
        Exiled.Events.Handlers.Server.ReportingCheater -= OnReportingCheater;
        Exiled.Events.Handlers.Server.LocalReporting -= OnLocalReporting;
        Exiled.Events.Handlers.Server.RestartingRound -= OnRestartingRound;
        Exiled.Events.Handlers.Player.Dying -= OnDying;
        Exiled.Events.Handlers.Player.Kicking -= OnKicking;
        Exiled.Events.Handlers.Player.Kicked -= OnKicked;
        Exiled.Events.Handlers.Player.Banned -= OnBanned;
        PendingKickIssuers.Clear();
    }

    private static void OnRestartingRound()
    {
        // Kicking が発火してもキャンセルされ Kicked が来なかった場合の取りこぼし掃除
        PendingKickIssuers.Clear();
    }

    private static void OnKicking(KickingEventArgs ev)
    {
        if (ev.Target == null) return;
        PendingKickIssuers[ev.Target.UserId] = ev.Player;
    }

    private static void OnKicked(KickedEventArgs ev)
    {
        if (!ev.Player.IsSafePlayer()) return;

        Player issuer = null;
        if (PendingKickIssuers.TryGetValue(ev.Player.UserId, out issuer))
            PendingKickIssuers.Remove(ev.Player.UserId);

        bool hasIssuer = issuer != null && !issuer.IsHost;

        ModerationBridge.Send("kick", new
        {
            actor = hasIssuer ? issuer.Nickname : "サーバーコンソール",
            actorId = hasIssuer ? issuer.UserId : null,
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
