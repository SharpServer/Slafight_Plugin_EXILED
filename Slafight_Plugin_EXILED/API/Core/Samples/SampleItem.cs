using Exiled.API.Features;
using LabApi.Events.Arguments.PlayerEvents;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.API.Core.Samples;

/// <summary>
/// カスタムアイテムの書き方の見本です。
///
/// 見どころは <see cref="charges"/> が<b>ただのフィールド</b>であることです。
/// 追跡キーはシリアルで、それはインベントリのアイテムと地面のピックアップで共通なので、
/// 落として拾い直しても同じインスタンスが付いてきます。
/// シリアルをキーにした static 辞書を用意する必要はありません。
/// </summary>
/// <remarks>
/// 追加のイベントが必要なら RegisterEvents / UnregisterEvents を override します。
/// 追跡開始時に登録、Release・破棄・ラウンド再開時に解除され、落としても追跡は続きます。
/// イベント内では Check(ev.Item)、Check(ev.Pickup)、Check(serial) でこの個体かを判定できます。
/// Check(player) / CheckHeld(player) は装備中のアイテムを判定します。
/// </remarks>
public sealed class SampleItem : CustomUsableItem
{
    /// <summary>
    /// per-item 状態。static 辞書は要りません。
    /// </summary>
    private int charges = 3;

    public override ItemType BaseType => ItemType.Medkit;

    public override string Name => "Sample Medkit";

    public override string Description => $"動作確認用。残り {charges} 回。";

    protected override int MaximumUses => 3;

    protected override bool SuppressVanillaEffects => true;

    protected override void OnPickupCompleted(PlayerPickedUpItemEventArgs ev)
    {
        Log.Debug($"[Sample] {ev.Player?.Nickname} が {Name} を拾いました (残り {charges})。");
    }

    /// <summary>
    /// 使用モーションの完了時に、バニラの効果を差し替えます。
    /// </summary>
    protected override void OnCustomUse()
    {
        charges--;

        if (charges > 0)
        {
            Owner.ShowHint($"{Name}: 残り {charges} 回", 3f);
            return;
        }

        Owner.ShowHint($"{Name} を使い切りました。", 3f);
    }
}
