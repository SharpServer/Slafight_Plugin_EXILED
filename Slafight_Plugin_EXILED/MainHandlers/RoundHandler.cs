using System;
using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.SpecialEvents;
using Server = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.MainHandlers;

public class RoundHandler : IBootstrapHandler
{
    public static void Register()
    {
        Server.RoundStarted += OnRoundStarted;
        Server.RestartingRound += OnRoundRestarting;
        Server.WaitingForPlayers += OnWaitingForPlayers;
        SpawnSystem.Spawned += OnSpawnSystemSpawned;
    }

    public static void Unregister()
    {
        Server.RoundStarted -= OnRoundStarted;
        Server.RestartingRound -= OnRoundRestarting;
        Server.WaitingForPlayers -= OnWaitingForPlayers;
        SpawnSystem.Spawned -= OnSpawnSystemSpawned;
        ResetState(nameof(Unregister));
    }

    private static void OnRoundStarted()
    {
        ResetState(nameof(OnRoundStarted));
        EnsureFirstTeamOverridesRegistered();
        _guardsCoroutine = Timing.RunCoroutine(GuardsCoroutine());
    }

    private static void OnRoundRestarting()
        => ResetState(nameof(OnRoundRestarting));

    private static void OnWaitingForPlayers()
        => ResetState(nameof(OnWaitingForPlayers));

    private const float DefaultWaitForSpawnTime = 240f;

    public static float WaitForSpawnTime { get; private set; }
    public static float ElapsedTime { get; private set; }
    public static bool IsAlreadySpawned { get; private set; }

    private static CoroutineHandle _guardsCoroutine;
    private static Guid _firstTeamOverrideRegistrationId = Guid.Empty;
    private static bool _firstTeamSpawnRequested;

    public static bool IsSecurityTeamExpected()
    {
        var chaosSideCount = 0;
        var foundationSideCount = 0;

        foreach (var player in Player.List)
        {
            if (!ShouldCountForExpectedTeam(player))
                continue;

            switch (player.GetTeam())
            {
                case CTeam.ChaosInsurgency:
                case CTeam.ClassD:
                    chaosSideCount++;
                    break;
                case CTeam.FoundationForces:
                case CTeam.Scientists:
                case CTeam.Guards:
                    foundationSideCount++;
                    break;
            }
        }

        return chaosSideCount < foundationSideCount;
    }

    public static SpawnTypeId GetExpectedTeam()
    {
        if (IsSecurityTeamExpected())
        {
            return SpawnTypeId.SecurityTeam;
        }

        return SpawnTypeId.ChaosAgents;
    }

    private static IEnumerator<float> GuardsCoroutine()
    {
        ElapsedTime = 0f;
        while (true)
        {
            if (IsRoundHandlerSuppressed())
            {
                CancelFirstTeamOverrides("round handler suppressed");
                yield break;
            }

            if (IsAlreadySpawned)
                yield break;

            ElapsedTime += 0.1f;
            if (!_firstTeamSpawnRequested && ElapsedTime >= WaitForSpawnTime)
            {
                RefreshFirstTeamOverrides();

                // NtfMiniWave/ChaosMiniWave は RespawnTokens が 0 から始まり、
                // メインの Normal Wave (NtfWave/ChaosWave) が一度湧くまで解放されないため、
                // ラウンド開始直後の「最初の湧き」を強制する対象にはできない。
                // 最初に湧く Normal Wave 側のタイマーを進める。
                SpawnableFaction faction = IsSecurityTeamExpected()
                    ? SpawnableFaction.NtfWave
                    : SpawnableFaction.ChaosWave;

                Respawn.AdvanceTimer(faction, 999);
                _firstTeamSpawnRequested = true;
                Log.Debug($"RoundHandler: requested first-team spawn via {faction}.");
            }
            yield return Timing.WaitForSeconds(0.1f);
        }
    }

    private static void EnsureFirstTeamOverridesRegistered()
    {
        if (_firstTeamOverrideRegistrationId != Guid.Empty || IsAlreadySpawned || IsRoundHandlerSuppressed())
            return;

        _firstTeamOverrideRegistrationId = SpawnSystem.AddNextSpawnOverrides(
            [
                new SpawnSystem.NextSpawnOverride(SpawnTypeId.SecurityTeam)
                {
                    SourceSpawnableFaction = SpawnableFaction.NtfWave,
                    Priority = 1000,
                },
                new SpawnSystem.NextSpawnOverride(SpawnTypeId.ChaosAgents)
                {
                    SourceSpawnableFaction = SpawnableFaction.ChaosWave,
                    Priority = 1000,
                }
            ],
            nameof(RoundHandler));
    }

    private static void RefreshFirstTeamOverrides()
    {
        CancelFirstTeamOverrides("refresh before first-team spawn request");
        EnsureFirstTeamOverridesRegistered();
    }

    private static void OnSpawnSystemSpawned(object sender, SpawnSystem.CustomSpawningEventArgs ev)
    {
        if (IsAlreadySpawned || !ev.SpawnType.HasValue || !IsFirstTeamSchedulerActive())
            return;

        // SecurityTeam/ChaosAgents 以外の湧き（Predicate 不一致などで
        // オーバーライドが未消費のまま別の湧きが発生した場合）で
        // スケジューラを完了扱いにしないようにする。
        if (ev.SpawnType.Value is not (SpawnTypeId.SecurityTeam or SpawnTypeId.ChaosAgents))
            return;

        CompleteFirstTeamSpawn(ev.SpawnType.Value);
    }

    private static bool IsFirstTeamSchedulerActive()
    {
        return _firstTeamOverrideRegistrationId != Guid.Empty || _firstTeamSpawnRequested;
    }

    private static void CompleteFirstTeamSpawn(SpawnTypeId spawnType)
    {
        IsAlreadySpawned = true;
        _firstTeamSpawnRequested = false;
        CancelFirstTeamOverrides("first team spawned");

        if (_guardsCoroutine.IsRunning)
            Timing.KillCoroutines(_guardsCoroutine);

        Log.Debug($"RoundHandler: first-team scheduler completed by spawn {spawnType}.");
    }

    private static void CancelFirstTeamOverrides(string reason)
    {
        if (_firstTeamOverrideRegistrationId == Guid.Empty)
            return;

        SpawnSystem.RemoveNextSpawnOverride(_firstTeamOverrideRegistrationId, reason);
        _firstTeamOverrideRegistrationId = Guid.Empty;
    }

    private static bool IsRoundHandlerSuppressed()
    {
        if (Round.IsLobby)
            return true;

        var specialEventsHandler = SpecialEventsHandler.Instance;
        if (specialEventsHandler == null)
            return false;

        return specialEventsHandler.NowEvent is SpecialEventType.FacilityTermination ||
               specialEventsHandler.EventQueue.Count > 0 &&
               specialEventsHandler.EventQueue[0] is SpecialEventType.FacilityTermination;
    }

    private static bool ShouldCountForExpectedTeam(Player player)
    {
        return player != null
               && player.ReferenceHub != null
               && player.IsNotHost()
               && player.Role.Type is not RoleTypeId.None and not RoleTypeId.Spectator;
    }

    private static void ResetState(string reason)
    {
        if (_guardsCoroutine.IsRunning)
            Timing.KillCoroutines(_guardsCoroutine);

        WaitForSpawnTime = DefaultWaitForSpawnTime;
        ElapsedTime = 0f;
        IsAlreadySpawned = false;
        _firstTeamSpawnRequested = false;
        CancelFirstTeamOverrides(reason);
        Log.Debug($"RoundHandler: reset state ({reason}).");
    }
}
