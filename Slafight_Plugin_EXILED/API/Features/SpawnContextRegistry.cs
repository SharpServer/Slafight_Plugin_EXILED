using System.Collections.Generic;
using Exiled.API.Features;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// SpawnContext の一元管理。
/// SpawnSystem やイベントから共通で利用。
/// </summary>
public static class SpawnContextRegistry
{
    private static readonly Dictionary<string, SpawnContext> Contexts = new();

    public static string ActiveContextName { get; private set; } = "Default";

    public static SpawnContext ActiveContext
    {
        get
        {
            if (Contexts.TryGetValue(ActiveContextName, out var ctx))
                return ctx;

            Log.Warn($"SpawnContextRegistry: Active context '{ActiveContextName}' not found.");
            return null;
        }
    }

    public static void Register(SpawnContext? context)
    {
        if (context == null || string.IsNullOrWhiteSpace(context.Name))
        {
            Log.Warn("SpawnContextRegistry: Tried to register null or unnamed context.");
            return;
        }

        Contexts[context.Name] = context;
        Log.Info($"SpawnContextRegistry: Context '{context.Name}' registered.");
    }

    public static bool Unregister(string name)
    {
        if (Contexts.Remove(name))
        {
            Log.Info($"SpawnContextRegistry: Context '{name}' unregistered.");
            return true;
        }

        return false;
    }

    public static bool TryGet(string name, out SpawnContext context)
        => Contexts.TryGetValue(name, out context);

    public static void SetActive(string name)
    {
        if (!Contexts.ContainsKey(name))
        {
            Log.Warn($"SpawnContextRegistry: Unknown context '{name}'");
            return;
        }

        ActiveContextName = name;
        Log.Info($"SpawnContextRegistry: Active context switched to '{name}'");
    }

    public static void Clear()
    {
        Contexts.Clear();
        ActiveContextName = "Default";
    }
}