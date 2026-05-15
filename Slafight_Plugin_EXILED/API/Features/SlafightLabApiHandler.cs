using System;
using LabApi.Events.CustomHandlers;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// Base class for LabAPI custom handlers used by this plugin.
/// Put all LabAPI/EXILED event subscriptions in <see cref="RegisterEvents"/> through
/// <see cref="EventSubscriptionScope.Add"/> so registration and cleanup stay paired.
/// </summary>
public abstract class SlafightLabApiHandler : CustomEventsHandler, IDisposable
{
    private bool _enabled;

    protected EventSubscriptionScope Subscriptions { get; } = new();

    internal void Enable()
    {
        if (_enabled)
            return;

        try
        {
            RegisterEvents(Subscriptions);
            CustomHandlersManager.RegisterEventsHandler(this);
            _enabled = true;
        }
        catch
        {
            Subscriptions.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (!_enabled)
            return;

        try
        {
            CustomHandlersManager.UnregisterEventsHandler(this);
        }
        finally
        {
            Subscriptions.Dispose();
            OnDisposed();
            _enabled = false;
        }
    }

    protected abstract void RegisterEvents(EventSubscriptionScope subscriptions);

    protected virtual void OnDisposed()
    {
    }
}
