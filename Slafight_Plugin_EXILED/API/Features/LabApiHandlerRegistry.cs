namespace Slafight_Plugin_EXILED.API.Features;

public static class LabApiHandlerRegistry
{
    public static T Register<T>(T? current = null) where T : SlafightLabApiHandler, new()
    {
        if (current != null)
            return current;

        var handler = new T();
        handler.Enable();
        return handler;
    }

    public static void Unregister<T>(ref T instance) where T : SlafightLabApiHandler
    {
        instance?.Dispose();
        instance = null;
    }
}
