#nullable enable
using System;
using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using Exiled.API.Structs;
using InventorySystem.Items.Armor;
using UnityEngine;

using PlayerEvents = Exiled.Events.EventArgs.Player;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// Armor 系 CItem の中間基底。
///
/// <para>
/// <b>CItem との差分：</b><br/>
/// • <see cref="VestEfficacy"/> / <see cref="HelmetEfficacy"/> / <see cref="StaminaUseMultiplier"/>
///   を Armor 固有プロパティとして定義し、<see cref="CustomizeItem"/> で焼き付ける。<br/>
/// • <see cref="AmmoLimits"/> / <see cref="CategoryLimits"/> を追加
///   （<see cref="Exiled.CustomItems.API.Features.CustomArmor"/> 互換）。<br/>
/// • <see cref="Give"/> を override して <see cref="Armor"/> を直接 Create → カスタマイズ →
///   <see cref="Player.AddItem(Item)"/> する経路に変更。これにより Give 時にも
///   全プロパティが確実に反映される。<br/>
/// • <see cref="CreatePickupForSpawn"/> を override して
///   Item.Create → カスタマイズ → CreatePickup の経路を使う（Spawn 時に効力値を保持）。<br/>
/// • <see cref="RegisterEvents"/> / <see cref="UnregisterEvents"/> で
///   <see cref="OnPickingUp"/> の Armor 特殊処理（BodyArmorPickup 防止）を自動登録。
/// </para>
///
/// <para>
/// 派生クラスは <see cref="CItem.UniqueKey"/>・<see cref="CItem.BaseItem"/> と
/// 必要なプロパティを override するだけで動く。
/// </para>
/// </summary>
public abstract class CItemArmor : CItem
{
    // ======================================================
    // Armor カスタマイズ設定
    // ======================================================

    /// <summary>ベスト防弾効率（0–100）。</summary>
    protected virtual int VestEfficacy => 80;

    /// <summary>ヘルメット防弾効率（0–100）。</summary>
    protected virtual int HelmetEfficacy => 80;

    /// <summary>スタミナ消耗倍率（1.0 ≦ 値 ≦ 2.0 推奨）。</summary>
    protected virtual float StaminaUseMultiplier => 1.15f;

    /// <summary>
    /// 弾薬所持上限リスト。空なら変更しない。
    /// <see cref="Exiled.CustomItems.API.Features.CustomArmor.AmmoLimits"/> 互換。
    /// </summary>
    protected virtual IReadOnlyList<ArmorAmmoLimit> AmmoLimits { get; } = [];

    /// <summary>
    /// アイテムカテゴリ上限リスト。空なら変更しない。
    /// <see cref="Exiled.CustomItems.API.Features.CustomArmor.CategoryLimits"/> 互換。
    /// </summary>
    protected virtual IReadOnlyList<BodyArmor.ArmorCategoryLimitModifier> CategoryLimits { get; }
        = [];

    // ======================================================
    // CustomizeItem — Armor 値の焼き付け
    // ======================================================

    /// <inheritdoc/>
    /// <remarks>
    /// <see cref="Armor"/> として Cast できる場合に効力値・弾薬上限・カテゴリ上限を適用する。
    /// 派生クラスが追加処理を行う場合は必ず <c>base.CustomizeItem(item)</c> を呼ぶこと。
    /// </remarks>
    protected override void CustomizeItem(Item item)
    {
        base.CustomizeItem(item);

        if (item is not Armor armor) return;

        armor.VestEfficacy         = VestEfficacy;
        armor.HelmetEfficacy       = HelmetEfficacy;
        armor.StaminaUseMultiplier = StaminaUseMultiplier;

        if (AmmoLimits.Count > 0)
            armor.AmmoLimits = AmmoLimits;

        if (CategoryLimits.Count > 0)
            armor.CategoryLimits = CategoryLimits;
    }

    // ======================================================
    // Give — Armor を直接 Create してカスタマイズ後に AddItem
    // ======================================================

    /// <inheritdoc/>
    /// <remarks>
    /// <see cref="Armor"/> を直接 <see cref="Item.Create"/> してカスタマイズしてから
    /// <see cref="Player.AddItem(Item)"/> する。
    /// これにより <c>AddItem(ItemType)</c> が返す素の Armor を上書きする手順を省き、
    /// 全プロパティを Give 時点で確実に反映する。
    /// </remarks>
    public override Item? Give(Player? player, bool displayMessage = false)
    {
        if (player == null) return null;

        CItem.SetPendingGive(this, displayMessage);
        try
        {
            var armor = (Armor?)Item.Create(BaseItem);
            if (armor == null) return null;

            CustomizeItem(armor);

            // Player.AddItem(Item) は void を返すため、
            // armor 自体がすでに正しいシリアルを持っている点を利用してそのまま返す。
            player.AddItem(armor);

            if (!CItem.SerialTracker.TryGet(armor.Serial, out _))
            {
                CItem.SerialTracker.ForceRegister(armor.Serial, this);
                Log.Warn($"[CItemArmor] Give: ItemAdded missed for serial={armor.Serial}, force-registered.");
            }

            return armor;
        }
        catch (Exception ex)
        {
            Log.Error($"CItemArmor.Give failed ({GetType().Name}): {ex}");
            return null;
        }
        finally
        {
            CItem.ClearPendingGive();
        }
    }

    // ======================================================
    // Spawn — Item.Create → CustomizeItem → CreatePickup
    // ======================================================

    /// <inheritdoc/>
    /// <remarks>
    /// Spawn 経路でも <see cref="CustomizeItem"/> を確実に通すため
    /// <c>Item.Create → CreatePickup</c> の二段階を使う。
    /// </remarks>
    protected override Pickup? CreatePickupForSpawn(Vector3 position)
    {
        var item = Item.Create(BaseItem);
        if (item == null) return null;

        CustomizeItem(item);
        return item.CreatePickup(position);
    }

    // ======================================================
    // PickingUpItem — BodyArmorPickup 経由を防止して Give に差し替え
    // ======================================================

    /// <summary>
    /// Exiled の <see cref="Exiled.CustomItems.API.Features.CustomArmor"/> と同様の処理。
    /// プレイヤーが床の Armor Pickup を拾おうとした際、
    /// <c>BodyArmorPickup</c> の生処理（素のステータスで装着）を阻止し、
    /// <see cref="Give"/> で正しくカスタマイズ済みの Armor を付与し直す。
    /// </summary>
    protected override void OnPickingUp(PlayerEvents.PickingUpItemEventArgs ev)
    {
        // インベントリ満杯なら素通り（EXILED デフォルト動作に任せる）
        if (ev.Player.Items.Count >= 8) return;

        // BodyArmorPickup でなければ（通常の Pickup 型）素通り
        if (ev.Pickup is not Exiled.API.Features.Pickups.BodyArmorPickup) return;

        // キャンセルして Give に差し替え
        ev.IsAllowed = false;
        ev.Pickup.Destroy();
        Give(ev.Player, displayMessage: true);
    }

    // ======================================================
    // RegisterEvents / UnregisterEvents は CItem の自動ディスパッチに任せるため不要。
    // OnPickingUp は CItem の静的ハンドラ経由で呼ばれる。
    // ======================================================
}