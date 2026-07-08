using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.MainHandlers;

/// <summary>
/// 通報(F7チーター報告 / ローカル通報)とチームキル(FF)を検知し、
/// <see cref="ModerationBridge"/> 経由で Discord Bot 側へ通知する。
/// Warn/Kick/Ban は実行者が確定している ModeratorUtil 側から直接通知する。
/// </summary>
public static class ModerationEventsHandler
{
    public static void Register()
    {
        Exiled.Events.Handlers.Server.ReportingCheater += OnReportingCheater;
        Exiled.Events.Handlers.Server.LocalReporting += OnLocalReporting;
        Exiled.Events.Handlers.Player.Dying += OnDying;
    }

    public static void Unregister()
    {
        Exiled.Events.Handlers.Server.ReportingCheater -= OnReportingCheater;
        Exiled.Events.Handlers.Server.LocalReporting -= OnLocalReporting;
        Exiled.Events.Handlers.Player.Dying -= OnDying;
    }

    private static void OnReportingCheater(ReportingCheaterEventArgs ev)
    {
        if (ev.Player == null || ev.Target == null) return;

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
        if (ev.Player == null || ev.Target == null) return;

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
        if (attacker == null || victim == null || attacker == victim) return;

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
