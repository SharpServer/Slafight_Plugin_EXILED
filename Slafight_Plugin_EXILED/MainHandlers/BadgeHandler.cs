using Exiled.Events.EventArgs.Player;

namespace Slafight_Plugin_EXILED.MainHandlers;

public class BadgeHandler : System.IDisposable
{
    private bool _disposed;

    public BadgeHandler()
    {
        Exiled.Events.Handlers.Player.Verified += _BadgeHandler;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Exiled.Events.Handlers.Player.Verified -= _BadgeHandler;
        System.GC.SuppressFinalize(this);
    }

    public void _BadgeHandler(VerifiedEventArgs ev)
    {
        long steamId = long.Parse(ev.Player.RawUserId.Split('@')[0]);
    }
}
