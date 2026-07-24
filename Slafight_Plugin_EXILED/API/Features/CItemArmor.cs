#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using Exiled.API.Structs;
using InventorySystem.Items.Armor;
using PlayerStatsSystem;
using UnityEngine;
using BodyArmorPickup = Exiled.API.Features.Pickups.BodyArmorPickup;
using PlayerEvents = Exiled.Events.EventArgs.Player;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// CItemArmor の防護計算方式。
/// </summary>
public enum ArmorProtectionMode
{
    /// <summary>
    /// ゲーム標準の弾道防護のみを使用する。
    /// 実際の軽減率は「Efficacy × (1 - 攻撃側の貫通率)」。
    /// </summary>
    VanillaBallistic,

    /// <summary>
    /// 貫通率に左右されない、設定値どおりの固定割合軽減を使用する。
    /// このモードではゲーム標準の Efficacy は 0 にされる。
    /// </summary>
    FixedPercentage,

    /// <summary>
    /// ゲーム標準の弾道防護を適用した上で、固定割合軽減も適用する。
    /// 強力になりやすいため、意図して併用する場合のみ使用する。
    /// </summary>
    Combined,
}

/// <summary>
/// Armor 系 CItem の中間基底。
///
/// <para>
/// <b>CItem との差分：</b><br/>
/// • 標準弾道防護と固定割合軽減を <see cref="ProtectionMode"/> で明示的に選択できる。<br/>
/// • 重量・移動速度・スタミナ消耗・民間クラスのデメリット倍率を個別調整できる。<br/>
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
/// 固定軽減率を使う場合は次のように指定する。
/// <code>
/// protected override ArmorProtectionMode ProtectionMode => ArmorProtectionMode.FixedPercentage;
/// protected override float VestDamageReductionPercent => 35f;
/// protected override float HelmetDamageReductionPercent => 50f;
/// </code>
/// </para>
/// </summary>
public abstract class CItemArmor : CItem
{
    // ======================================================
    // Armor カスタマイズ設定
    // ======================================================

    /// <summary>
    /// 防護計算方式。
    /// 固定の分かりやすい軽減率を使う場合は <see cref="ArmorProtectionMode.FixedPercentage"/>。
    /// </summary>
    protected virtual ArmorProtectionMode ProtectionMode => ArmorProtectionMode.VanillaBallistic;

    /// <summary>
    /// ベストのゲーム標準弾道防護値（0–100）。
    /// 実際の弾丸軽減率は「この値 × (1 - 弾丸の貫通率)」になる。
    /// </summary>
    protected virtual int VestBallisticEfficacy => VestEfficacy;

    /// <summary>
    /// ヘルメットのゲーム標準弾道防護値（0–100）。
    /// 実際の弾丸軽減率は「この値 × (1 - 弾丸の貫通率)」になる。
    /// </summary>
    protected virtual int HelmetBallisticEfficacy => HelmetEfficacy;

    /// <summary>
    /// 互換用の旧ベスト防弾効率。新規実装では <see cref="VestBallisticEfficacy"/> を使用する。
    /// </summary>
    protected virtual int VestEfficacy => 80;

    /// <summary>
    /// 互換用の旧ヘルメット防弾効率。新規実装では <see cref="HelmetBallisticEfficacy"/> を使用する。
    /// </summary>
    protected virtual int HelmetEfficacy => 80;

    /// <summary>胴体への固定ダメージ軽減率（0–100%）。</summary>
    protected virtual float VestDamageReductionPercent => 0f;

    /// <summary>頭部への固定ダメージ軽減率（0–100%）。</summary>
    protected virtual float HelmetDamageReductionPercent => 0f;

    /// <summary>手足への固定ダメージ軽減率（0–100%）。</summary>
    protected virtual float LimbDamageReductionPercent => VestDamageReductionPercent;

    /// <summary>
    /// Hitbox を持たないダメージへの固定軽減率（0–100%）。
    /// 爆発・落下・SCP 攻撃などを一括で軽減したい場合に使う。
    /// </summary>
    protected virtual float GeneralDamageReductionPercent => 0f;

    /// <summary>
    /// ダメージ種別ごとの固定軽減率上書き（0–100%）。
    /// 指定された種別は Hitbox / General の設定より優先される。
    /// </summary>
    protected virtual IReadOnlyDictionary<DamageType, float> DamageReductionOverrides { get; }
        = new Dictionary<DamageType, float>();

    /// <summary>
    /// スタミナ消耗倍率。1.0 が通常、1未満で消耗軽減、1より大きいと消耗増加。
    /// </summary>
    protected virtual float StaminaUseMultiplier => 1.15f;

    /// <summary>
    /// 移動速度倍率。null ならベースアーマーの値を維持する。
    /// 1.0 が通常、0.9 なら移動速度90%。
    /// </summary>
    protected virtual float? MovementSpeedMultiplier => null;

    /// <summary>アーマー重量。null ならベースアーマーの値を維持する。</summary>
    protected virtual float? ArmorWeight => null;

    /// <summary>
    /// Class-D / Scientist に適用される装備デメリット倍率。
    /// null ならベースアーマーの値を維持する。
    /// </summary>
    protected virtual float? CivilianDownsidesMultiplier => null;

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

    /// <summary>読みやすい弾薬上限定義を生成する。</summary>
    protected static ArmorAmmoLimit AmmoLimit(AmmoType ammoType, int limit)
        => new()
        {
            AmmoType = ammoType,
            Limit = (ushort)Mathf.Clamp(limit, 0, ushort.MaxValue),
        };

    /// <summary>読みやすいカテゴリ上限定義を生成する。</summary>
    protected static BodyArmor.ArmorCategoryLimitModifier CategoryLimit(ItemCategory category, int limit)
        => new()
        {
            Category = category,
            Limit = (byte)Mathf.Clamp(limit, 0, byte.MaxValue),
        };

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

        bool useVanillaProtection = ProtectionMode != ArmorProtectionMode.FixedPercentage;
        armor.VestEfficacy         = useVanillaProtection ? ClampPercent(VestBallisticEfficacy) : 0;
        armor.HelmetEfficacy       = useVanillaProtection ? ClampPercent(HelmetBallisticEfficacy) : 0;
        armor.StaminaUseMultiplier = StaminaUseMultiplier;

        if (MovementSpeedMultiplier.HasValue)
            armor.Base._movementSpeedMultiplier = Mathf.Max(0f, MovementSpeedMultiplier.Value);

        if (ArmorWeight.HasValue)
            armor.Weight = Mathf.Max(0f, ArmorWeight.Value);

        if (CivilianDownsidesMultiplier.HasValue)
            armor.Base.CivilianClassDownsidesMultiplier = Mathf.Max(0f, CivilianDownsidesMultiplier.Value);

        if (AmmoLimits.Count > 0)
            armor.AmmoLimits = AmmoLimits;

        if (CategoryLimits.Count > 0)
            armor.CategoryLimits = CategoryLimits;
    }

    // ======================================================
    // 固定割合ダメージ軽減
    // ======================================================

    /// <inheritdoc/>
    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Player.Hurting += OnArmorOwnerHurting;
        base.RegisterEvents();
    }

    /// <inheritdoc/>
    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Player.Hurting -= OnArmorOwnerHurting;
        base.UnregisterEvents();
    }

    /// <summary>
    /// この攻撃に適用する固定軽減率を返す。
    /// 派生クラスで攻撃者・距離・武器などを使った独自判定に差し替えられる。
    /// </summary>
    protected virtual float GetDamageReductionPercent(PlayerEvents.HurtingEventArgs ev)
    {
        if (DamageReductionOverrides.TryGetValue(ev.DamageHandler.Type, out float overridden))
            return overridden;

        if (ev.DamageHandler.Base is not FirearmDamageHandler firearm)
            return GeneralDamageReductionPercent;

        return firearm.Hitbox switch
        {
            HitboxType.Headshot => HelmetDamageReductionPercent,
            HitboxType.Limb     => LimbDamageReductionPercent,
            _                   => VestDamageReductionPercent,
        };
    }

    /// <summary>
    /// この攻撃に固定軽減を適用するか。
    /// 派生クラスで攻撃者・距離・役職などを条件にしたい場合に override する。
    /// </summary>
    protected virtual bool CanReduceDamage(PlayerEvents.HurtingEventArgs ev) => true;

    /// <summary>
    /// 固定軽減が適用された直後のフック。
    /// </summary>
    protected virtual void OnDamageReduced(
        PlayerEvents.HurtingEventArgs ev,
        float originalDamage,
        float reducedDamage)
    {
    }

    private void OnArmorOwnerHurting(PlayerEvents.HurtingEventArgs ev)
    {
        if (ProtectionMode == ArmorProtectionMode.VanillaBallistic)
            return;

        if (ev?.Player == null || !ev.IsAllowed)
            return;

        if (ev.IsInstantKill)
            return;

        if (ev.Amount <= 0f || !CanReduceDamage(ev))
            return;

        // ゲーム本体もインベントリ内の最初の BodyArmor を装備中アーマーとして扱う。
        // 同じ順序で判定し、複数アーマーによる軽減の重複適用を防ぐ。
        var wornArmor = ev.Player.Items.OfType<Armor>().FirstOrDefault();
        if (!Check(wornArmor))
            return;

        float originalDamage = ev.Amount;
        float reduction = ClampPercent(GetDamageReductionPercent(ev)) / 100f;
        float reducedDamage = Mathf.Max(0f, originalDamage * (1f - reduction));

        ev.Amount = reducedDamage;
        OnDamageReduced(ev, originalDamage, reducedDamage);
    }

    private static int ClampPercent(int value)
        => Mathf.Clamp(value, 0, 100);

    private static float ClampPercent(float value)
        => Mathf.Clamp(value, 0f, 100f);

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

        SetPendingGive(this, displayMessage);
        try
        {
            var armor = (Armor?)Item.Create(BaseItem);
            if (armor == null) return null;

            CustomizeItem(armor);

            // Player.AddItem(Item) は void を返すため、
            // armor 自体がすでに正しいシリアルを持っている点を利用してそのまま返す。
            player.AddItem(armor);

            if (!SerialTracker.TryGet(armor.Serial, out _))
            {
                SerialTracker.ForceRegister(armor.Serial, this);
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
            ClearPendingGive();
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
        if (ev.Pickup is not BodyArmorPickup) return;

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
