using System;
using System.Collections.Generic;
using Exiled.API.Features;

namespace Slafight_Plugin_EXILED.API.Features;

public sealed class EventSubscriptionScope : IDisposable
{
    private readonly List<Action> _unsubscribers = new();
    private bool _disposed;

    public void Add(Action subscribe, Action unsubscribe)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EventSubscriptionScope));

        subscribe();
        _unsubscribers.Add(unsubscribe);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        for (var i = _unsubscribers.Count - 1; i >= 0; i--)
        {
            try
            {
                _unsubscribers[i]();
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to unsubscribe event: {ex}");
            }
        }

        _unsubscribers.Clear();
    }
}
