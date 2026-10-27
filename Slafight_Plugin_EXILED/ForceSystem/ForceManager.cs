using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.ForceSystem.Forces;

using ExiledSpawnedEventArgs = Exiled.Events.EventArgs.Player.SpawnedEventArgs;
using PlayerHandlers = Exiled.Events.Handlers.Player;
using ServerHandlers = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.ForceSystem;

/// <summary>
/// 部隊システムの入口です。波が出たら隊を作り、評価ループを回します。
/// </summary>
public sealed class ForceManager : EventHandlerBase
{
    /// <inheritdoc />
    public override void RegisterEvents()
    {
        // 名札の %extrainfo% に部隊名と階級を差し込む。
        CustomInfoDisplay.ExtraInfoProvider = ForceNameplate.Text;

        SpawnSystem.Spawned += OnSpawned;
        PlayerHandlers.Spawned += OnRoleSpawned;
        ServerHandlers.RestartingRound += OnRestartingRound;
        ServerHandlers.WaitingForPlayers += OnRestartingRound;
        ServerHandlers.RoundStarted += OnRoundStarted;
    }

    /// <inheritdoc />
    public override void UnregisterEvents()
    {
        if (CustomInfoDisplay.ExtraInfoProvider == ForceNameplate.Text)
            CustomInfoDisplay.ExtraInfoProvider = null;

        SpawnSystem.Spawned -= OnSpawned;
        PlayerHandlers.Spawned -= OnRoleSpawned;
        ServerHandlers.RestartingRound -= OnRestartingRound;
        ServerHandlers.WaitingForPlayers -= OnRestartingRound;
        ServerHandlers.RoundStarted -= OnRoundStarted;

        ForceRegistry.Reset();
    }

    /// <summary>
    /// ラウンド開始時に評価ループを起こします。
    /// </summary>
    private static void OnRoundStarted() => ForceEvaluator.Start();

    private static void OnRestartingRound() => ForceRegistry.Reset();

    /// <summary>
    /// 役職が変わった人の所属を見直します。
    /// </summary>
    private static void OnRoleSpawned(ExiledSpawnedEventArgs ev)
    {
        if (ev?.Player is not { } player) return;

        ForceRegistry.Refresh(player);
    }

    /// <summary>
    /// 波が出たら、その波ぶんの隊を 1 つ作ります。
    /// </summary>
    private static void OnSpawned(SpawnedEventArgs ev)
    {
        if (ev?.Players is not { Count: > 0 }) return;

        // 波が自分で名乗るならそれが最優先。名乗らなければ陣営から決める。
        ForceBase force = ev.Wave?.CreateForce(ev.UnitId, ev.UnitName)
                          ?? Create(ev.Wave?.RespawnFaction ?? Faction.Unclassified, ev.UnitId, ev.UnitName);

        if (force is null) return;

        ForceRegistry.Register(force);

        foreach (Player player in ev.Players)
        {
            if (ForceRegistry.MemberOf(player) is not { } member) continue;

            force.Add(member);
        }

        ForceEvaluator.AssignTopLead(force);
    }

    /// <summary>
    /// 陣営に合った隊を作ります。部隊システムの対象外なら null。
    /// </summary>
    private static ForceBase Create(Faction faction, byte? unitId, string unitName)
    {
        switch (faction)
        {
            case Faction.FoundationStaff:
                // NTF はバニラが採番した部隊名をそのまま名乗る。名札との食い違いを避けるため。
                return new MobileTaskForce(unitName, unitId);

            case Faction.FoundationEnemy:
                return new ChaosForce();

            default:
                return null;
        }
    }
}
