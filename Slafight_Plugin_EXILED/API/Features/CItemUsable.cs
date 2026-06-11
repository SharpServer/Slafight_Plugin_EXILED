#nullable enable
using System.Collections.Generic;
using Exiled.API.Features.Items;

using PlayerEvents = Exiled.Events.EventArgs.Player;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// 使用完了型 CItem の中間基底。
/// UsingItemCompleted で既定使用をキャンセルし、派生クラスの効果・使用回数・消費処理をまとめて扱う。
/// </summary>
public abstract class CItemUsable : CItem
{
    private readonly Dictionary<ushort, int> _remainingUsesBySerial = new();

    /// <summary>
    /// 1 個あたりの使用回数。0 以下なら無制限。
    /// </summary>
    protected virtual int MaxUseCount => 1;

    /// <summary>
    /// UsingItemCompleted で EXILED/ゲーム側の既定使用処理を止める。
    /// </summary>
    protected virtual bool CancelDefaultUse => true;

    /// <summary>
    /// 使用回数が尽きたとき、所持アイテムを destroy 付きで削除する。
    /// </summary>
    protected virtual bool DestroyItemWhenUsesDepleted => true;

    /// <summary>
    /// true の場合は、使用回数が尽きても使用効果だけは実行してアイテムを残す。
    /// </summary>
    protected virtual bool AllowEffectAfterUsesDepleted => false;

    protected bool UsesAreLimited => MaxUseCount > 0;

    protected int GetRemainingUses(Item? item)
    {
        if (item == null) return 0;
        return GetRemainingUses(item.Serial);
    }

    protected int GetRemainingUses(ushort serial)
    {
        if (!UsesAreLimited) return int.MaxValue;
        EnsureUseCounter(serial);
        return _remainingUsesBySerial[serial];
    }

    protected void SetRemainingUses(Item? item, int uses)
    {
        if (item == null || !UsesAreLimited) return;
        _remainingUsesBySerial[item.Serial] = uses < 0 ? 0 : uses;
    }

    protected override void OnAcquired(PlayerEvents.ItemAddedEventArgs ev, bool displayMessage)
    {
        base.OnAcquired(ev, displayMessage);
        if (ev.Item != null)
            EnsureUseCounter(ev.Item.Serial);
    }

    protected override void OnSpawned(Exiled.API.Features.Pickups.Pickup pickup)
    {
        base.OnSpawned(pickup);
        EnsureUseCounter(pickup.Serial);
    }

    protected override void OnUsing(PlayerEvents.UsingItemEventArgs ev)
    {
        base.OnUsing(ev);

        if (ev.Player == null || ev.Item == null) return;

        EnsureUseCounter(ev.Item.Serial);

        if (CanStartUse(ev)) return;

        ev.IsAllowed = false;
        OnStartUseDenied(ev);
    }

    protected override void OnUsingItemCompleted(PlayerEvents.UsingItemCompletedEventArgs ev)
    {
        base.OnUsingItemCompleted(ev);

        if (ev.Player == null || ev.Item == null) return;

        if (CancelDefaultUse)
            ev.IsAllowed = false;

        EnsureUseCounter(ev.Item.Serial);

        if (!CanUse(ev))
        {
            OnUseDenied(ev);
            return;
        }

        if (UsesAreLimited && GetRemainingUses(ev.Item.Serial) <= 0 && !AllowEffectAfterUsesDepleted)
        {
            OnUsesDepleted(ev);
            return;
        }

        OnUsedEffect(ev);
        ConsumeUse(ev);
    }

    protected override void OnWaitingForPlayers()
    {
        base.OnWaitingForPlayers();
        _remainingUsesBySerial.Clear();
    }

    /// <summary>
    /// Using 段階で使用行動を開始できるかを派生クラスで判定する。
    /// false の場合は ev.IsAllowed を false にし、後続の使用シーケンスには入らない想定。
    /// </summary>
    protected virtual bool CanStartUse(PlayerEvents.UsingItemEventArgs ev) => true;

    /// <summary>
    /// CanStartUse が false を返したときに呼ばれる。
    /// </summary>
    protected virtual void OnStartUseDenied(PlayerEvents.UsingItemEventArgs ev) { }

    /// <summary>
    /// UsingItemCompleted 段階で効果を発動できるかを派生クラスで判定する。
    /// false の場合、効果も消費も行わない。
    /// </summary>
    protected virtual bool CanUse(PlayerEvents.UsingItemCompletedEventArgs ev) => true;

    /// <summary>
    /// CanUse が false を返したときに呼ばれる。
    /// </summary>
    protected virtual void OnUseDenied(PlayerEvents.UsingItemCompletedEventArgs ev) { }

    /// <summary>
    /// 使用効果本体。派生クラスは基本的にここだけ override すればよい。
    /// </summary>
    protected virtual void OnUsedEffect(PlayerEvents.UsingItemCompletedEventArgs ev) { }

    /// <summary>
    /// 使用回数が 0 の状態で使用完了イベントが来たとき、または消費後に 0 になったときに呼ばれる。
    /// </summary>
    protected virtual void OnUsesDepleted(PlayerEvents.UsingItemCompletedEventArgs ev)
    {
        if (!DestroyItemWhenUsesDepleted || ev.Player == null || ev.Item == null) return;

        _remainingUsesBySerial.Remove(ev.Item.Serial);
        SerialTracker.ForceUnregister(ev.Item.Serial);
        ev.Player.RemoveItem(ev.Item, destroy: true);
    }

    /// <summary>
    /// 使用回数を 1 減らす。派生クラスで独自消費にしたい場合だけ override する。
    /// </summary>
    protected virtual void ConsumeUse(PlayerEvents.UsingItemCompletedEventArgs ev)
    {
        if (!UsesAreLimited || ev.Item == null) return;

        int remaining = GetRemainingUses(ev.Item.Serial) - 1;
        _remainingUsesBySerial[ev.Item.Serial] = remaining < 0 ? 0 : remaining;

        if (remaining <= 0)
            OnUsesDepleted(ev);
    }

    private void EnsureUseCounter(ushort serial)
    {
        if (!UsesAreLimited) return;
        if (!_remainingUsesBySerial.ContainsKey(serial))
            _remainingUsesBySerial[serial] = MaxUseCount;
    }
}
