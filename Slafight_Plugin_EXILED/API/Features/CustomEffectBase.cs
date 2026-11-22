using System;
using CustomPlayerEffects;
using Exiled.API.Features;

namespace Slafight_Plugin_EXILED.API.Features;

public abstract class CustomEffectBase : StatusEffectBase
{
    /// <summary>
    /// このエフェクトインスタンスを所持しているプレイヤー。
    /// </summary>
    public Player Player { get; private set; }

    private bool eventsRegistered;

    public override void Enabled()
    {
        base.Enabled();
        Player = Player.Get(Hub);

        if (eventsRegistered)
            return;

        eventsRegistered = true;
        try
        {
            RegisterEvents();
        }
        catch
        {
            RemoveEventHandlers();
            Player = null;
            throw;
        }
    }

    public override void Disabled()
    {
        base.Disabled();
        RemoveEventHandlers();
        Player = null;
    }

    /// <summary>
    /// プレイヤー切断などで <see cref="Disabled"/> が呼ばれずに破棄された場合の漏れ対策。
    /// </summary>
    public virtual void OnDestroy()
    {
        RemoveEventHandlers();
    }

    /// <summary>
    /// エフェクトの有効化時にイベントを購読します。
    /// </summary>
    /// <remarks>ハンドラは全プレイヤー分発火するので、<c>ev.Player == Player</c> で絞り込むこと。</remarks>
    protected virtual void RegisterEvents() { }

    /// <summary>
    /// 無効化・破棄・登録失敗時にイベント購読を解除します。
    /// </summary>
    protected virtual void UnregisterEvents() { }

    private void RemoveEventHandlers()
    {
        if (!eventsRegistered)
            return;

        eventsRegistered = false;
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
