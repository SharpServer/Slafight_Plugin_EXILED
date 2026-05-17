using System.Linq;
using System.Reflection;
using Slafight_Plugin_EXILED.API.Interface;

namespace Slafight_Plugin_EXILED.MainHandlers;

public static class AutoHandlerBootstrapRegister
{
    public static void Register()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var interfaceType = typeof(IBootstrapHandler);

        var handlerTypes = assembly
            .GetTypes()
            .Where(t =>
                interfaceType.IsAssignableFrom(t) &&
                t.IsClass &&
                !t.IsAbstract);

        foreach (var handlerType in handlerTypes)
        {
            var registerMethod = handlerType.GetMethod(
                "Register",
                BindingFlags.Public | BindingFlags.Static);

            if (registerMethod == null)
                continue;

            if (registerMethod.ReturnType != typeof(void) ||
                registerMethod.GetParameters().Length != 0)
                continue;

            registerMethod.Invoke(null, null);
        }
    }

    public static void Unregister()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var interfaceType = typeof(IBootstrapHandler);

        var handlerTypes = assembly
            .GetTypes()
            .Where(t =>
                interfaceType.IsAssignableFrom(t) &&
                t.IsClass &&
                !t.IsAbstract);

        foreach (var handlerType in handlerTypes)
        {
            var unregisterMethod = handlerType.GetMethod(
                "Unregister",
                BindingFlags.Public | BindingFlags.Static);

            if (unregisterMethod == null)
                continue;

            if (unregisterMethod.ReturnType != typeof(void) ||
                unregisterMethod.GetParameters().Length != 0)
                continue;

            unregisterMethod.Invoke(null, null);
        }
    }
}
