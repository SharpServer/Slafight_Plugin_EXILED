using System;
using System.Linq;
using System.Reflection;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Interface;

namespace Slafight_Plugin_EXILED.MainHandlers;

public static class AutoHandlerBootstrapRegister
{
    public static void Register()
    {
        foreach (var handlerType in GetHandlerTypes())
        {
            var registerMethod = handlerType.GetMethod(
                "Register",
                BindingFlags.Public | BindingFlags.Static);

            if (registerMethod == null)
                continue;

            if (registerMethod.ReturnType != typeof(void) ||
                registerMethod.GetParameters().Length != 0)
                continue;

            try
            {
                registerMethod.Invoke(null, null);
                Log.Debug($"AutoHandlerBootstrapRegister: registered {handlerType.FullName}");
            }
            catch (TargetInvocationException ex)
            {
                Log.Error($"AutoHandlerBootstrapRegister: Register failed for {handlerType.FullName}: {ex.InnerException ?? ex}");
            }
            catch (Exception ex)
            {
                Log.Error($"AutoHandlerBootstrapRegister: Register failed for {handlerType.FullName}: {ex}");
            }
        }
    }

    public static void Unregister()
    {
        foreach (var handlerType in GetHandlerTypes().Reverse())
        {
            var unregisterMethod = handlerType.GetMethod(
                "Unregister",
                BindingFlags.Public | BindingFlags.Static);

            if (unregisterMethod == null)
                continue;

            if (unregisterMethod.ReturnType != typeof(void) ||
                unregisterMethod.GetParameters().Length != 0)
                continue;

            try
            {
                unregisterMethod.Invoke(null, null);
                Log.Debug($"AutoHandlerBootstrapRegister: unregistered {handlerType.FullName}");
            }
            catch (TargetInvocationException ex)
            {
                Log.Error($"AutoHandlerBootstrapRegister: Unregister failed for {handlerType.FullName}: {ex.InnerException ?? ex}");
            }
            catch (Exception ex)
            {
                Log.Error($"AutoHandlerBootstrapRegister: Unregister failed for {handlerType.FullName}: {ex}");
            }
        }
    }

    private static IOrderedEnumerable<Type> GetHandlerTypes()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var interfaceType = typeof(IBootstrapHandler);

        return assembly
            .GetTypes()
            .Where(t =>
                interfaceType.IsAssignableFrom(t) &&
                t.IsClass &&
                !t.IsAbstract)
            .OrderBy(GetBootstrapOrder);
    }

    private static int GetBootstrapOrder(Type type)
    {
        // SpawnSystem owns the static spawn events. Subscribers must register after it,
        // otherwise SpawnSystem.Register() clears their event subscriptions.
        if (type == typeof(SpawnSystem))
            return -100;

        if (type == typeof(SpawningHandler))
            return -90;

        return 0;
    }
}
