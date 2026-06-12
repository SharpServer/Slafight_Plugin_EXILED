using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.SpecialEvents;

namespace Slafight_Plugin_EXILED.MainHandlers;

public class RoundHandler : IBootstrapHandler
{
    public static void Register()
    {
        Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;
        Exiled.Events.Handlers.Server.RestartingRound += OnRoundRestarting;
        Exiled.Events.Handlers.Server.WaitingForPlayers += OnWaitingForPlayers;
    }

    public static void Unregister()
    {
        Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;
        Exiled.Events.Handlers.Server.RestartingRound -= OnRoundRestarting;
        Exiled.Events.Handlers.Server.WaitingForPlayers -= OnWaitingForPlayers;
        ResetState(nameof(Unregister));
    }

    private static void OnRoundStarted()
    {
        ResetState(nameof(OnRoundStarted));
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
        else
        {
            return SpawnTypeId.ChaosAgents;
        }
    }

    private static IEnumerator<float> GuardsCoroutine()
    {
        ElapsedTime = 0f;
        while (true)
        {
            if (Round.IsLobby || SpecialEventsHandler.Instance.NowEvent is SpecialEventType.FacilityTermination)
                yield break;

            ElapsedTime += 0.1f;
            if (ElapsedTime >= WaitForSpawnTime)
            {
                SpawnableFaction faction;
                if (IsSecurityTeamExpected())
                {
                    faction = SpawnableFaction.NtfMiniWave;
                    SpawnSystem.ReplaceNextSpawn(SpawnTypeId.SecurityTeam, source: nameof(RoundHandler));
                }
                else
                {
                    faction = SpawnableFaction.ChaosMiniWave;
                    SpawnSystem.ReplaceNextSpawn(SpawnTypeId.ChaosAgents, source: nameof(RoundHandler));
                }

                Respawn.AdvanceTimer(faction, 999);
                IsAlreadySpawned = true;
                yield break;
            }
            yield return Timing.WaitForSeconds(0.1f);
        }
    }

    private static bool ShouldCountForExpectedTeam(Player player)
    {
        return player != null
               && player.IsConnected
               && player.ReferenceHub != null
               && !player.IsHost
               && !player.ReferenceHub.IsHost
               && player.Role.Type is not RoleTypeId.None and not RoleTypeId.Spectator;
    }

    private static void ResetState(string reason)
    {
        if (_guardsCoroutine.IsRunning)
            Timing.KillCoroutines(_guardsCoroutine);

        WaitForSpawnTime = DefaultWaitForSpawnTime;
        ElapsedTime = 0f;
        IsAlreadySpawned = false;
        Log.Debug($"RoundHandler: reset state ({reason}).");
    }
}
