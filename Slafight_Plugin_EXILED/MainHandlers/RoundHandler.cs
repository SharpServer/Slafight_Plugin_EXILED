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
    }

    public static void Unregister()
    {
        Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;
    }

    private static void OnRoundStarted()
    {
        IsAlreadySpawned = false;
        WaitForSpawnTime = 240f;
        Timing.RunCoroutine(GuardsCoroutine());
    }

    public static float WaitForSpawnTime { get; private set; }
    public static float ElapsedTime { get; private set; }
    public static bool IsAlreadySpawned { get; private set; }

    public static bool IsSecurityTeamExpected()
    {
        var chaosSideCount = 0;
        var foundationSideCount = 0;

        foreach (var player in Player.List)
        {
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
            if (Round.IsLobby || SpecialEventsHandler.Instance.NowEvent is SpecialEventType.FacilityTermination) yield break;
            ElapsedTime += 0.1f;
            if (ElapsedTime >= WaitForSpawnTime)
            {
                SpawnableFaction faction;
                if (IsSecurityTeamExpected())
                {
                    faction = SpawnableFaction.NtfMiniWave;
                    SpawnSystem.ReplaceNextSpawn(SpawnTypeId.SecurityTeam);
                }
                else
                {
                    faction = SpawnableFaction.ChaosMiniWave;
                    SpawnSystem.ReplaceNextSpawn(SpawnTypeId.ChaosAgents);
                }

                Respawn.AdvanceTimer(faction, 999);
                IsAlreadySpawned = true;
                yield break;
            }
            yield return Timing.WaitForSeconds(0.1f);
        }
    }
}
