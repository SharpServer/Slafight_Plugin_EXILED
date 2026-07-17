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
/// (デコンパイルで確認済み)。
/// 注意: 実際の Ban (duration&gt;0) でも BanUser の最後で ServerConsole.Disconnect が直接呼ばれるため、
/// Banned に加えて Kicked も必ず発火する(デコンパイルで確認済み)。そのままだと同じ操作が
/// Ban通知とKick通知の二重で飛んでしまうため、Banned側で対象を記録して Kicked側で抑制する。
/// Kicked(post) には実行者情報が無いため、Kicking(pre, 実行者を含む) を一時対応付けて合成する
/// (実際のKick=duration=0はこの経路のみを通り、Bannedは発火しない)。
/// Warn は既存フレームワークにイベントが無いため、実行者が確定している ModeratorUtil 側から直接通知する。
/// </summary>
public static class ModerationEventsHandler
{
    // Kicking(pre) で捕捉した実行者を Kicked(post) 発火まで一時保持する。Key: 対象のUserId
    private static readonly Dictionary<string, Player> PendingKickIssuers = new();

    // Banned で処理済みの対象。直後に付随して発火する Kicked を二重通知しないための抑制用。Key: 対象のUserId
    private static readonly HashSet<string> SuppressNextKick = [];

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
        SuppressNextKick.Clear();
    }

    private static void OnRestartingRound()
    {
        // Kicking/Banned が発火しても対になるイベントが来なかった場合の取りこぼし掃除
        PendingKickIssuers.Clear();
        SuppressNextKick.Clear();
    }

    private static void OnKicking(KickingEventArgs ev)
    {
        if (ev.Target == null) return;
        PendingKickIssuers[ev.Target.UserId] = ev.Player;
    }

    private static void OnKicked(KickedEventArgs ev)
    {
        if (!ev.Player.IsSafePlayer()) return;

        // 直前の Banned 通知に付随する強制切断なので、Kick として二重通知しない
        if (SuppressNextKick.Remove(ev.Player.UserId))
        {
            PendingKickIssuers.Remove(ev.Player.UserId);
            return;
        }

        Player issuer = null;
        PendingKickIssuers.TryGetValue(ev.Player.UserId, out issuer);
        PendingKickIssuers.Remove(ev.Player.UserId);

        var (actorName, actorId) = FormatActor(issuer);

        ModerationBridge.Send("kick", new
        {
            actor = actorName,
            actorId,
            target = ev.Player.Nickname,
            targetId = ev.Player.UserId,
            reason = ev.Reason,
        });
    }

    private static void OnBanned(BannedEventArgs ev)
    {
        if (!ev.Target.IsSafePlayer()) return;

        // この直後に必ず Kicked (強制切断) も発火するため、そちらは無視させる
        SuppressNextKick.Add(ev.Target.UserId);

        var details = ev.Details;
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

        // ev.Player は Banned イベントでは "実行者" を指す (Target が対象)。
        // Details.Issuer の文字列パースより、Exiled が解決済みの Player を使う方がシンプルで確実。
        var (actorName, actorId) = FormatActor(ev.Player);

        ModerationBridge.Send("ban", new
        {
            actor = actorName,
            actorId,
            target = ev.Target.Nickname,
            targetId = ev.Target.UserId,
            duration = durationLabel,
            reason,
            banType = ev.Type.ToString(),
            forced = ev.IsForced,
        });
    }

    private static (string Name, string Id) FormatActor(Player issuer)
    {
        if (issuer == null || issuer.IsHost)
            return ("サーバーコンソール", null);

        return (issuer.Nickname, issuer.UserId);
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
