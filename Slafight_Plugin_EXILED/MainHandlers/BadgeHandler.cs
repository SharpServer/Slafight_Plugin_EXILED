using System;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.Handlers;

namespace Slafight_Plugin_EXILED.MainHandlers;

public class BadgeHandler : IDisposable
{
    private bool _disposed;

    public BadgeHandler()
    {
        Player.Verified += _BadgeHandler;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Player.Verified -= _BadgeHandler;
        GC.SuppressFinalize(this);
    }

    public void _BadgeHandler(VerifiedEventArgs ev)
    {
        long steamId = long.Parse(ev.Player.RawUserId.Split('@')[0]);
    }
}
