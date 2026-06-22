using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Features;
using HarmonyLib;
using MEC;

namespace Slafight_Plugin_EXILED.Patches;

/// <summary>
/// Gives effect state changes time to reach clients before an NPC is destroyed.
/// Effects remain fully usable until destruction has actually been requested.
/// </summary>
public static class NpcEffectCleanupState
{
    public const float DestroyDelay = 0.25f;

    private static readonly HashSet<int> PendingDestroyIds = [];
    private static readonly HashSet<int> ReadyDestroyIds = [];

    public static bool IsPending(ReferenceHub? hub)
    {
        if (hub == null)
            return false;

        return PendingDestroyIds.Contains(hub.PlayerId);
    }

    public static bool BeginDestroy(Npc npc)
    {
        int playerId = npc.Id;

        if (ReadyDestroyIds.Remove(playerId))
        {
            PendingDestroyIds.Remove(playerId);
            return true;
        }

        if (!PendingDestroyIds.Add(playerId))
            return false;

        try
        {
            npc.DisableAllEffects();
        }
        catch (System.Exception ex)
        {
            Log.Warn($"[NpcEffectCleanup] Failed to disable effects for NPC {playerId}: {ex.Message}");
        }

        Timing.CallDelayed(DestroyDelay, () =>
        {
            Npc? current = Npc.Get(playerId);
            if (current?.ReferenceHub == null)
            {
                PendingDestroyIds.Remove(playerId);
                ReadyDestroyIds.Remove(playerId);
                return;
            }

            ReadyDestroyIds.Add(playerId);
            current.Destroy();
        });

        return false;
    }
}

[HarmonyPatch(typeof(Npc), nameof(Npc.Destroy))]
public static class NpcDestroyEffectCleanupPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Npc __instance)
        => __instance == null || NpcEffectCleanupState.BeginDestroy(__instance);
}

[HarmonyPatch(typeof(StatusEffectBase), nameof(StatusEffectBase.ServerSetState))]
public static class NpcPendingDestroyEffectPatch
{
    [HarmonyPrefix]
    private static bool Prefix(StatusEffectBase __instance, byte intensity)
    {
        if (intensity == 0 || __instance?.Hub == null)
            return true;

        return !NpcEffectCleanupState.IsPending(__instance.Hub);
    }
}
