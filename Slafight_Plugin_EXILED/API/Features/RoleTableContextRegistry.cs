using System.Collections.Generic;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Features.RoleTableDictionaries.Contexts;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// RoleTableContext の一元管理。
/// 初期ロール配布やイベントから共通で利用。
/// </summary>
public static class RoleTableContextRegistry
{
    private const string DefaultContextName = NormalRoleTableContext.ContextName;

    private static readonly Dictionary<string, RoleTableContext> Contexts = new();

    static RoleTableContextRegistry()
    {
        RegisterDefaults();
    }

    public static string ActiveContextName { get; private set; } = DefaultContextName;

    public static RoleTableContext ActiveContext
    {
        get
        {
            if (Contexts.TryGetValue(ActiveContextName, out var context))
                return context;

            Log.Warn($"RoleTableContextRegistry: Active context '{ActiveContextName}' not found. Fallback to '{DefaultContextName}'.");
            return Contexts[DefaultContextName];
        }
    }

    public static void Register(RoleTableContext? context)
    {
        if (context == null || string.IsNullOrWhiteSpace(context.Name))
        {
            Log.Warn("RoleTableContextRegistry: Tried to register null or unnamed context.");
            return;
        }

        Contexts[context.Name] = context;
    }

    public static bool Unregister(string name)
        => Contexts.Remove(name);

    public static bool TryGet(string name, out RoleTableContext context)
        => Contexts.TryGetValue(name, out context);

    public static void SetActive(string name)
    {
        if (!Contexts.ContainsKey(name))
        {
            Log.Warn($"RoleTableContextRegistry: Unknown context '{name}'. Fallback to '{DefaultContextName}'.");
            ActiveContextName = DefaultContextName;
            return;
        }

        ActiveContextName = name;
    }

    public static void Clear()
    {
        Contexts.Clear();
        RegisterDefaults();
        ActiveContextName = DefaultContextName;
    }

    private static void RegisterDefaults()
    {
        Register(new NormalRoleTableContext());
        Register(new AprilRoleTableContext());
    }
}
