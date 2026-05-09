using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.Extensions;

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
        Timing.RunCoroutine(GuardsCoroutine());
    }

    private static IEnumerator<float> GuardsCoroutine()
    {
        float elapsedTime = 0f;
        while (true)
        {
            if (Round.IsLobby) yield break;
            elapsedTime += 0.1f;
            if (elapsedTime >= 235f)
            {
                Faction faction;
                if (Player.List.Count(p => p.GetTeam() is CTeam.ChaosInsurgency or CTeam.ClassD) >= Player.List.Count(p => p.GetTeam() is CTeam.FoundationForces or CTeam.Scientists or CTeam.Guards))
                {
                    faction = Faction.FoundationEnemy;
                    SpawnSystem.ReplaceNextSpawn(SpawnTypeId.ChaosAgents);
                }
                else
                {
                    faction = Faction.FoundationStaff;
                    SpawnSystem.ReplaceNextSpawn(SpawnTypeId.SecurityTeam);
                }
                Respawn.AdvanceTimer(faction, 999);
            }
            yield return Timing.WaitForSeconds(0.1f);
        }
    }
}