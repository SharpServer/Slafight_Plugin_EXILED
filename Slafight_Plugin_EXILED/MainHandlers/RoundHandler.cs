using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.Extensions;
using Random = UnityEngine.Random;

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
        lwsRandom = Random.Range(0, 6);
        rrhRandom = Random.Range(0, 4);
        Timing.RunCoroutine(GuardsCoroutine());
    }

    public static float WaitForSpawnTime { get; private set; }
    public static float ElapsedTime { get; private set; }
    public static bool IsAlreadySpawned { get; private set; }
    private static int lwsRandom;
    private static int rrhRandom;

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

    public static bool IsRrhExpected()
    {
        var hasManager = Player.List.Any(p => p.GetCustomRole() is CRoleTypeId.FacilityManager);
        if (!hasManager) return false;
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

        if (foundationSideCount+2 >= chaosSideCount) return false;
        if (rrhRandom is 0)
        {
            return true;
        }
        return false;
    }
    
    public static bool IsLwsExpected()
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

        if (foundationSideCount+2 >= chaosSideCount) return false;
        if (lwsRandom is 0)
        {
            return true;
        }
        return false;
    }

    public static SpawnTypeId GetExpectedTeam()
    {
        if (IsLwsExpected())
        {
            return SpawnTypeId.MtfLwsNormal;
        }

        if (IsRrhExpected())
        {
            return SpawnTypeId.MtfRrhNormal;
        }

        return IsSecurityTeamExpected() ? SpawnTypeId.SecurityTeam : SpawnTypeId.ChaosAgents;
    }

    private static IEnumerator<float> GuardsCoroutine()
    {
        ElapsedTime = 0f;
        while (true)
        {
            if (Round.IsLobby) yield break;
            ElapsedTime += 0.1f;
            if (ElapsedTime >= WaitForSpawnTime)
            {
                Faction faction;
                if (IsLwsExpected())
                {
                    faction = Faction.FoundationStaff;
                    SpawnSystem.ReplaceNextSpawn(SpawnTypeId.MtfLwsNormal);
                }
                else if (IsRrhExpected())
                {
                    faction = Faction.FoundationStaff;
                    SpawnSystem.ReplaceNextSpawn(SpawnTypeId.MtfRrhNormal);
                }
                else
                {
                    if (IsSecurityTeamExpected())
                    {
                        faction = Faction.FoundationStaff;
                        SpawnSystem.ReplaceNextSpawn(SpawnTypeId.SecurityTeam);
                    }
                    else
                    {
                        faction = Faction.FoundationEnemy;
                        SpawnSystem.ReplaceNextSpawn(SpawnTypeId.ChaosAgents);
                    }
                }
                Log.Debug($"[FIRST SPAWN]Next expected spawn: {SpawnSystem.PendingOverrideType}");

                Respawn.GrantTokens(faction, 1);
                Respawn.AdvanceTimer(faction, 999);
                IsAlreadySpawned = true;
                yield break;
            }
            yield return Timing.WaitForSeconds(0.1f);
        }
    }
}
