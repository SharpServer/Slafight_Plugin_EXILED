using System;
using System.Collections.Generic;
using Exiled.API.Features;
using HarmonyLib;
using InventorySystem.Items.Usables.Scp330;
using MapGeneration.Holidays;

namespace Slafight_Plugin_EXILED.Patches;

[HarmonyPatch(typeof(Scp330Candies), nameof(Scp330Candies.Candies), MethodType.Getter)]
public static class CandyPatch
{
    private static ICandy[] _normalSet;
    private static ICandy[] _halloweenSet;

    private static bool Prefix(ref ICandy[] __result)
    {
        bool halloween = HolidayUtils.IsHolidayActive(HolidayType.Halloween);
        __result = halloween
            ? _halloweenSet ??= FixCandies(true)
            : _normalSet ??= FixCandies(false);
        return false;
    }

    private static ICandy[] FixCandies(bool preferHaunted)
    {
        var asm = typeof(Scp330Candies).Assembly;

        // Kind ごとに候補を集める
        var normals = new Dictionary<CandyKindID, ICandy>();
        var haunteds = new Dictionary<CandyKindID, ICandy>();

        foreach (Type t in asm.GetTypes())
        {
            if (!typeof(ICandy).IsAssignableFrom(t) || t.IsInterface || t.IsAbstract)
                continue;

            try
            {
                if (Activator.CreateInstance(t) is not ICandy candy)
                    continue;

                string typeName = t.FullName ?? t.Name;
                bool isHaunted = typeName.Contains("Haunted", StringComparison.OrdinalIgnoreCase);

                if (isHaunted)
                {
                    haunteds[candy.Kind] = candy;
                    Log.Info($"[CandyPatch] Haunted candidate: {candy.Kind} ({typeName})");
                }
                else
                {
                    normals[candy.Kind] = candy;
                    Log.Info($"[CandyPatch] Normal candidate: {candy.Kind} ({typeName})");
                }
            }
            catch (Exception e)
            {
                Log.Warn($"[CandyPatch] Failed to create {t.FullName}: {e}");
            }
        }

        // ハロウィン中は Haunted を優先、それ以外は通常を優先（通常が無い Kind のみ Haunted）
        var primary = preferHaunted ? haunteds : normals;
        var fallback = preferHaunted ? normals : haunteds;
        var result = new List<ICandy>(primary.Values);

        foreach (var kv in fallback)
        {
            if (!primary.ContainsKey(kv.Key))
                result.Add(kv.Value);
        }

        Log.Info($"[CandyPatch] preferHaunted={preferHaunted}: {string.Join(", ", result.ConvertAll(c => $"{c.Kind}({c.GetType().Name})"))}");

        return result.ToArray();
    }

}