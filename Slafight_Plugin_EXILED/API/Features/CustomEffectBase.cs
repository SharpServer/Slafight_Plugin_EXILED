using System;
using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Features;

namespace Slafight_Plugin_EXILED.API.Features;

public abstract class CustomEffectBase : StatusEffectBase
{
    /// <summary>
    /// このエフェクトインスタンスを所持しているプレイヤー。
    /// </summary>
    public Player Player { get; private set; }

    /// <summary>
    /// <see cref="RegisterEvent"/> で登録された購読解除アクション。
    /// エフェクト無効化 / 破棄時に自動実行される。
    /// </summary>
    private readonly List<Action> _eventCleanup = [];

    public override void Enabled()
    {
        base.Enabled();
        Player = Player.Get(Hub);
        SubscribeEvents();
    }

    public override void Disabled()
    {
        base.Disabled();
        RunEventCleanup();
        Player = null;
    }

    /// <summary>
    /// プレイヤー切断などで <see cref="Disabled"/> が呼ばれずに破棄された場合の漏れ対策。
    /// </summary>
    public virtual void OnDestroy()
    {
        RunEventCleanup();
    }

    /// <summary>
    /// 有効化のたびに呼ばれる。購読は <see cref="RegisterEvent"/> で登録する。
    /// </summary>
    /// <remarks>ハンドラは全プレイヤー分発火するので、<c>ev.Player == Player</c> で絞り込むこと。</remarks>
    protected virtual void SubscribeEvents()
    {
    }

    /// <summary>
    /// <paramref name="subscribe"/> を即時実行し、<paramref name="unsubscribe"/> を無効化 / 破棄時に実行する。
    /// </summary>
    protected void RegisterEvent(Action subscribe, Action unsubscribe)
    {
        subscribe?.Invoke();

        if (unsubscribe != null)
            _eventCleanup.Add(unsubscribe);
    }

    private void RunEventCleanup()
    {
        if (_eventCleanup.Count == 0)
            return;

        foreach (Action unsubscribe in _eventCleanup)
        {
            try
            {
                unsubscribe();
            }
            catch (Exception ex)
            {
                Log.Error($"[{GetType().Name}] Failed to unsubscribe event: {ex}");
            }
        }

        _eventCleanup.Clear();
    }
}
