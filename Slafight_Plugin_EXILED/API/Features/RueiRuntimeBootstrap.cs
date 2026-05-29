using System;
using System.Reflection;
using Exiled.API.Features;
using RueI.API;

namespace Slafight_Plugin_EXILED.API.Features;

public static class RueiRuntimeBootstrap
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
            return;

        if (!TryInvokeRueDisplayLifecycle("RegisterEvents"))
            return;

        _registered = true;
        Log.Debug("[RueI] Runtime events registered by Slafight.");
    }

    public static void Unregister()
    {
        if (!_registered)
            return;

        TryInvokeRueDisplayLifecycle("UnregisterEvents");
        _registered = false;
        Log.Debug("[RueI] Runtime events unregistered by Slafight.");
    }

    private static bool TryInvokeRueDisplayLifecycle(string methodName)
    {
        try
        {
            MethodInfo? method = typeof(RueDisplay).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);

            if (method == null)
            {
                Log.Warn($"[RueI] RueDisplay.{methodName} was not found.");
                return false;
            }

            method.Invoke(null, null);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn($"[RueI] RueDisplay.{methodName} failed: {ex.Message}");
            return false;
        }
    }
}
