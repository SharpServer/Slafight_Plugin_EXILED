using System;
using Exiled.API.Features;
using LabApi.Events.CustomHandlers;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// LabAPI のイベントオーバーライドと、追加のイベント購読を管理します。
/// </summary>
public abstract class SlafightLabApiHandler : CustomEventsHandler, IDisposable
{
    private bool _enabled;

    internal void Enable()
    {
        if (_enabled)
            return;

        try
        {
            RegisterEvents();
            CustomHandlersManager.RegisterEventsHandler(this);
            _enabled = true;
        }
        catch
        {
            try
            {
                RemoveEventHandlers();
            }
            catch (Exception exception)
            {
                Log.Error($"[{GetType().Name}] Failed to roll back LabAPI event registration: {exception}");
            }

            throw;
        }
    }

    public void Dispose()
    {
        if (!_enabled)
            return;

        _enabled = false;
        try
        {
            RemoveEventHandlers();
        }
        finally
        {
            OnDisposed();
        }
    }

    /// <summary>LabAPI が自動で登録するイベントオーバーライドに加え、必要なイベントを購読します。</summary>
    protected virtual void RegisterEvents() { }

    /// <summary>追加のイベント購読を解除します。登録に失敗した場合も呼ばれます。</summary>
    protected virtual void UnregisterEvents() { }

    protected virtual void OnDisposed()
    {
    }

    private void RemoveEventHandlers()
    {
        try
        {
            CustomHandlersManager.UnregisterEventsHandler(this);
        }
        finally
        {
            try
            {
                UnregisterEvents();
            }
            catch (Exception exception)
            {
                Log.Error($"[{GetType().Name}] Failed to unsubscribe events: {exception}");
            }
        }
    }
}
