using System;
using System.Collections.Generic;
using HarmonyLib;
using InventorySystem.Items.Firearms.Modules.Scp127;
using InventorySystem.Items.Scp1509;
using PlayerStatsSystem;

namespace Slafight_Plugin_EXILED.Patches;

/// <summary>
/// Prevents item-owned Hume Shield sessions from destroying an explicitly configured shield.
/// </summary>
/// <remarks>
/// SCP-127 and SCP-1509 use the player's shared <see cref="HumeShieldStat"/> for their shield.
/// Their session loops therefore clamp that stat to the item's maximum while equipped and drain
/// it to zero after holstering. EXILED's <c>MaxHumeShield</c> setter marks the stat with
/// <see cref="HumeShieldStat._maxValueOverride"/> specifically to make its configured maximum
/// authoritative, but those item loops bypass the normal <see cref="HumeShieldStat.Update"/>
/// handling and overwrite the current value directly.
///
/// Temporarily hide only those sessions from the item update. Keeping the sessions alive means
/// vanilla behavior can resume if another plugin later releases its maximum override. The items
/// themselves remain fully usable, while players without an explicit maximum retain the vanilla
/// item shield.
/// </remarks>
internal static class ItemHumeShieldOverrideProtection
{
    public static bool HasExplicitMaximum(HumeShieldStat stat) =>
        stat is not null && stat._maxValueOverride;
}

[HarmonyPatch(typeof(Scp127HumeModule), nameof(Scp127HumeModule.ServerUpdateSessions))]
internal static class Scp127HumeShieldOverridePatch
{
    [HarmonyPrefix]
    private static void Prefix(out List<Scp127HumeModule.HumeShieldSession> __state)
    {
        __state = [];

        for (int index = Scp127HumeModule.ServerActiveSessions.Count - 1; index >= 0; index--)
        {
            Scp127HumeModule.HumeShieldSession session = Scp127HumeModule.ServerActiveSessions[index];
            if (!ItemHumeShieldOverrideProtection.HasExplicitMaximum(session.Stat))
                continue;

            if (session._lastModule is { } module)
                module.HsRegeneration = 0f;

            __state.Add(session);
            Scp127HumeModule.ServerActiveSessions.RemoveAt(index);
        }
    }

    [HarmonyFinalizer]
    private static Exception Finalizer(
        Exception __exception,
        List<Scp127HumeModule.HumeShieldSession> __state)
    {
        if (__state is { Count: > 0 })
            Scp127HumeModule.ServerActiveSessions.AddRange(__state);

        return __exception;
    }
}

[HarmonyPatch(typeof(Scp1509Item), nameof(Scp1509Item.ServerUpdateSessions))]
internal static class Scp1509HumeShieldOverridePatch
{
    [HarmonyPrefix]
    private static void Prefix(out List<Scp1509Item.HumeShieldSession> __state)
    {
        __state = [];

        for (int index = Scp1509Item.ServerActiveSessions.Count - 1; index >= 0; index--)
        {
            Scp1509Item.HumeShieldSession session = Scp1509Item.ServerActiveSessions[index];
            if (!ItemHumeShieldOverrideProtection.HasExplicitMaximum(session.Stat))
                continue;

            if (session._lastItem is { } item)
                item.HsRegeneration = 0f;

            __state.Add(session);
            Scp1509Item.ServerActiveSessions.RemoveAt(index);
        }
    }

    [HarmonyFinalizer]
    private static Exception Finalizer(
        Exception __exception,
        List<Scp1509Item.HumeShieldSession> __state)
    {
        if (__state is { Count: > 0 })
            Scp1509Item.ServerActiveSessions.AddRange(__state);

        return __exception;
    }
}
