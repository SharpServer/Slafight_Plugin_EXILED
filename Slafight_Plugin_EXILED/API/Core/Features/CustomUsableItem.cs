using System;
using LabApi.Events.Arguments.PlayerEvents;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// 使用回数・使用可否・バニラ効果の差し替えを提供するカスタム使用アイテム基底です。
/// 状態はアイテムインスタンス自身に保持されるため、シリアル別辞書は不要です。
/// </summary>
public abstract class CustomUsableItem : CustomItem
{
    private int usesRemaining;

    private sealed class UsableState
    {
        public UsableState(int usesRemaining)
        {
            UsesRemaining = usesRemaining;
        }

        public int UsesRemaining { get; }
    }

    /// <summary>最大使用回数。0 以下なら回数を管理しません。</summary>
    protected virtual int MaximumUses => 0;

    /// <summary>バニラの使用効果を止め、<see cref="OnCustomUse"/> だけを実行するか。</summary>
    protected virtual bool SuppressVanillaEffects => false;

    /// <summary>最後の使用後にアイテムを破棄するか。</summary>
    protected virtual bool DestroyWhenDepleted => true;

    /// <summary>残り使用回数。回数を管理しない場合は -1。</summary>
    public int UsesRemaining => MaximumUses > 0 ? usesRemaining : -1;

    protected override void OnTrackingStarted()
    {
        usesRemaining = MaximumUses;
        base.OnTrackingStarted();
    }

    protected override object OnCaptureState(LabApi.Features.Wrappers.Item item)
        => MaximumUses > 0 ? new UsableState(usesRemaining) : null;

    protected override void OnRestoreState(LabApi.Features.Wrappers.Item item, object state)
    {
        if (MaximumUses > 0 && state is UsableState saved)
            usesRemaining = Math.Max(0, Math.Min(MaximumUses, saved.UsesRemaining));
    }

    protected override void OnUseStarting(PlayerUsingItemEventArgs ev)
    {
        if (!CanStartUse(ev))
        {
            ev.IsAllowed = false;
            OnUseStartDenied(ev);
        }

        base.OnUseStarting(ev);
    }

    protected override void OnUseEffectsApplying(PlayerItemUsageEffectsApplyingEventArgs ev)
    {
        if (!CanCompleteUse(ev))
        {
            ev.IsAllowed = false;
            OnUseCompletionDenied(ev);
        }
        else if (SuppressVanillaEffects)
        {
            ev.IsAllowed = false;
            CompleteUse();
        }

        base.OnUseEffectsApplying(ev);
    }

    protected override void OnUseCompleted(PlayerUsedItemEventArgs ev)
    {
        if (!SuppressVanillaEffects)
            CompleteUse();

        base.OnUseCompleted(ev);
    }

    /// <summary>使用開始を許可するか。</summary>
    protected virtual bool CanStartUse(PlayerUsingItemEventArgs ev) => UsesRemaining != 0;

    /// <summary>効果適用時点でも使用を許可するか。</summary>
    protected virtual bool CanCompleteUse(PlayerItemUsageEffectsApplyingEventArgs ev) => UsesRemaining != 0;

    /// <summary>使用開始を拒否したときに呼ばれます。</summary>
    protected virtual void OnUseStartDenied(PlayerUsingItemEventArgs ev)
    {
    }

    /// <summary>効果適用時点で使用を拒否したときに呼ばれます。</summary>
    protected virtual void OnUseCompletionDenied(PlayerItemUsageEffectsApplyingEventArgs ev)
    {
    }

    /// <summary>1 回分のカスタム効果を実行します。</summary>
    protected virtual void OnCustomUse()
    {
    }

    /// <summary>使用回数を使い切ったときに呼ばれます。</summary>
    protected virtual void OnUsesDepleted()
    {
    }

    private void CompleteUse()
    {
        OnCustomUse();

        if (MaximumUses <= 0)
            return;

        usesRemaining--;
        if (usesRemaining > 0)
            return;

        usesRemaining = 0;
        OnUsesDepleted();
        if (DestroyWhenDepleted)
            Destroy();
    }
}
