using System;
using LabApi.Events.CustomHandlers;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// LabAPI ハンドラの基底です。購読は <see cref="RegisterEvents"/> で <see cref="EventSubscriptionScope.Add"/> を通して登録します。
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
