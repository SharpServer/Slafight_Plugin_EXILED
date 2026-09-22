using System;
using System.Collections.Generic;
using System.Reflection;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.Scp914Events;
using LabApi.Events.Arguments.ServerEvents;
using LabApi.Events.Handlers;
using MEC;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Enums;
using UnityEngine;

using Log = Exiled.API.Features.Log;
using Player = Exiled.API.Features.Player;
using ExiledItem = Exiled.API.Features.Items.Item;
using Item = LabApi.Features.Wrappers.Item;
using Pickup = LabApi.Features.Wrappers.Pickup;
using LabPlayer = LabApi.Features.Wrappers.Player;
using LightSourceToy = LabApi.Features.Wrappers.LightSourceToy;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// カスタムアイテムです。<b>アイテム 1 個につき 1 インスタンス</b>生成されます。
/// フィールドをそのまま per-item 状態 (残弾・チャージ・使用回数など) として使えるので、
/// シリアルをキーにした static 辞書を自分で用意する必要はありません。
///
/// 追跡キーはシリアル (<see cref="ushort"/>) です。
/// シリアルはインベントリのアイテムと地面のピックアップで共通なので、
/// 落とす・拾い直すをまたいでも同じインスタンスが付いてきます。
///
/// 識別子の文字列も専用 enum もありません。<b>型そのものが同一性です。</b>
/// </summary>
/// <remarks>
/// <b>アイテムの実体とイベントだけは LabApi のものを使います。</b>
/// アーマー・SCP-330・使用効果の差し替えなど、EXILED 側に相当イベントが無いものが
/// あるためです。<see cref="Owner"/> をはじめプレイヤーを渡す口はすべて EXILED の
/// <see cref="Player"/> に揃えてあるので、機能側が LabApi を意識する必要はありません。
/// </remarks>
/// <example>
/// <code>
/// public sealed class MediPistol : CustomItem
/// {
///     public override ItemType BaseType => ItemType.GunCOM18;
///     public override string Name => "S41 医療用拳銃";
///
///     private int charges = 6;   // per-item 状態をそのままフィールドに持てる
///
///     protected override void OnShotCompleted(PlayerShotWeaponEventArgs ev)
///     {
///         if (charges-- &lt;= 0)
///             Owner.ShowHint("チャージ切れ", 3f);
///     }
/// }
///
/// CustomItem.Give&lt;MediPistol&gt;(player);
/// CustomItem.Spawn&lt;MediPistol&gt;(position);
/// </code>
/// </example>
public abstract class CustomItem
{
    private static readonly Dictionary<ushort, CustomItem> BySerial = new Dictionary<ushort, CustomItem>();

    private static bool hooked;

    private LightSourceToy? pickupLight;

    private SchematicObject? pickupSchematic;

    private ushort trackedSerial;

    // CustomHybrid のモード定義が、実際に紐付いた物理アイテムを参照するための文脈。
    private ushort hybridContextSerial;
    private Action hybridDestroyAction;
    private Action hybridRefreshPickupSchematicAction;

    /// <summary>
    /// このアイテムのシリアルです。生成時に決まります。
    /// </summary>
    public ushort Serial => trackedSerial != 0 ? trackedSerial : hybridContextSerial;

    /// <summary>
    /// 土台になるバニラアイテムです。
    /// </summary>
    public abstract ItemType BaseType { get; }

    /// <summary>
    /// 表示名です。既定はクラス名。
    /// </summary>
    public virtual string Name => GetType().Name;

    /// <summary>
    /// 説明です。
    /// </summary>
    public virtual string Description => string.Empty;

    /// <summary>
    /// このアイテム自身をカテゴリ所持上限へ数えないか。
    /// true のアイテムを所持している間は、そのカテゴリの有効上限へ 1 枠が加算されます。
    /// </summary>
    /// <remarks>
    /// 総インベントリ 8 枠の上限は変更しません。これは銃器・医療品などの
    /// <see cref="ItemCategory"/> ごとの制限だけを無視する設定です。
    /// </remarks>
    public virtual bool IgnoreCategoryLimits => false;

    /// <summary>
    /// 拾ったときに本人へ出す文言です。null / 空なら出しません。
    /// </summary>
    protected virtual string PickupHint => $"<size=24>あなたは{Name}を拾いました！\n{Description}</size>";

    /// <summary>
    /// 手に持ち替えたときに本人へ出す文言です。null / 空なら出しません。
    /// </summary>
    protected virtual string SelectedHint => $"<size=24>あなたは{Name}を選択しました！\n{Description}</size>";

    /// <summary><see cref="PickupHint"/> の表示秒数です。</summary>
    protected virtual float PickupHintDuration => 6f;

    /// <summary><see cref="SelectedHint"/> の表示秒数です。</summary>
    protected virtual float SelectedHintDuration => 5f;

    /// <summary>
    /// 地面に落ちている間、ピックアップを光らせるかどうか。
    /// 暗がりで見失いやすい特別なアイテムだけ true にします。
    /// </summary>
    protected virtual bool PickupLightEnabled => Rarity != Rarity.None;

    /// <summary>ピックアップライトの色です。</summary>
    protected virtual Color PickupLightColor => Rarity == Rarity.None ? Color.white : GetRarityColor(Rarity);

    /// <summary>ピックアップライトの強さです。</summary>
    protected virtual float PickupLightIntensity => 0.7f;

    /// <summary>ピックアップライトの届く距離です。</summary>
    protected virtual float PickupLightRange => 3.75f;

    /// <summary>ピックアップライトの影です。既定は影なし。</summary>
    protected virtual LightShadows PickupLightShadowType => LightShadows.None;

    /// <summary>
    /// 地面のピックアップに重ねる ProjectMER スキマティック名です。null / 空なら重ねません。
    /// </summary>
    /// <remarks>
    /// バニラの見た目を借りているアイテムに、本来の姿を被せるためのものです。
    /// 状態で見た目が変わるアイテム (レベルの上がる装置など) は、
    /// ここを自分の状態から組み立てて <see cref="RefreshPickupSchematic"/> を呼んでください。
    /// </remarks>
    protected virtual string PickupSchematicName => null;

    /// <summary>重ねるスキマティックの拡大率です。</summary>
    protected virtual Vector3 PickupSchematicScale => Vector3.one;

    /// <summary>
    /// アイテムのレアリティです。ライト未設定の時レアリティ標準ライトが使われます。
    /// </summary>
    public virtual Rarity Rarity => Rarity.None;

    /// <summary>
    /// インベントリ内の実体です。地面に落ちていれば null。
    /// </summary>
    public Item Item
    {
        get
        {
            ushort serial = Serial;
            if (serial == 0) return null;

            // シリアルキャッシュは生成直後だとまだ埋まっていないことがある。
            // EXILED が保持している実体から取得すれば LabApi 側のラッパーも同期的に作られる。
            return Item.Get(serial) ??
                   (ExiledItem.Get(serial) is { } item ? Item.Get(item.Base) : null);
        }
    }

    /// <summary>
    /// 地面のピックアップです。誰かが持っていれば null。
    /// </summary>
    public Pickup Pickup
    {
        get
        {
            ushort serial = Serial;
            if (serial == 0) return null;

            return Pickup.Get(serial) ??
                   (Exiled.API.Features.Pickups.Pickup.Get(serial) is { } pickup
                       ? Pickup.Get(pickup.Base)
                       : null);
        }
    }

    /// <summary>
    /// 現在の所持者です。地面にあれば null。
    /// </summary>
    public Player Owner => ToExiled(Item?.CurrentOwner);

    /// <summary>
    /// 追跡中のカスタムアイテムをすべて返します。
    /// </summary>
    public static IReadOnlyCollection<CustomItem> Tracked => BySerial.Values;

    /// <summary>
    /// 指定したアセンブリで定義されたカスタムアイテムの追跡だけを解除します。
    /// </summary>
    public static void ReleaseAssembly(Assembly assembly)
    {
        if (assembly is null) return;

        foreach (CustomItem custom in new List<CustomItem>(BySerial.Values))
        {
            if (custom.GetType().Assembly == assembly)
                custom.Release();
        }
    }

    /// <summary>
    /// 全アイテムを解放し、静的イベント購読を解除します。
    /// </summary>
    public static void Shutdown()
    {
        OnRoundRestarted();

        if (!hooked) return;

        PlayerEvents.PickedUpItem -= OnAnyPickedUp;
        PlayerEvents.PickedUpArmor -= OnAnyPickedUpArmor;
        PlayerEvents.PickedUpScp330 -= OnAnyPickedUpScp330;
        PlayerEvents.PickingUpItem -= OnAnyPickingUp;
        PlayerEvents.PickingUpArmor -= OnAnyPickingUpArmor;
        PlayerEvents.PickingUpScp330 -= OnAnyPickingUpScp330;
        PlayerEvents.DroppingItem -= OnAnyDropping;
        PlayerEvents.DroppedItem -= OnAnyDropped;
        PlayerEvents.ChangedItem -= OnAnyChangedItem;
        PlayerEvents.UsingItem -= OnAnyUsingItem;
        PlayerEvents.ItemUsageEffectsApplying -= OnAnyUsageEffectsApplying;
        PlayerEvents.UsedItem -= OnAnyUsedItem;
        PlayerEvents.CancellingUsingItem -= OnAnyCancellingUse;
        PlayerEvents.CancelledUsingItem -= OnAnyCancelledUse;
        PlayerEvents.ShootingWeapon -= OnAnyShootingWeapon;
        PlayerEvents.ShotWeapon -= OnAnyShotWeapon;
        PlayerEvents.DryFiringWeapon -= OnAnyDryFiringWeapon;
        PlayerEvents.DryFiredWeapon -= OnAnyDryFiredWeapon;
        PlayerEvents.ReloadingWeapon -= OnAnyReloadingWeapon;
        PlayerEvents.ReloadedWeapon -= OnAnyReloadedWeapon;
        PlayerEvents.ChangingAttachments -= OnAnyChangingAttachments;
        PlayerEvents.ChangedAttachments -= OnAnyChangedAttachments;
        PlayerEvents.Hurting -= OnAnyHurting;
        PlayerEvents.Dying -= OnAnyDying;
        PlayerEvents.ThrowingProjectile -= OnAnyThrowingProjectile;
        PlayerEvents.ThrewProjectile -= OnAnyThrewProjectile;
        PlayerEvents.InspectingKeycard -= OnAnyInspectingKeycard;
        PlayerEvents.InspectedKeycard -= OnAnyInspectedKeycard;
        Scp914Events.ProcessingPickup -= OnAnyScp914ProcessingPickup;
        Scp914Events.ProcessingInventoryItem -= OnAnyScp914ProcessingInventoryItem;
        ServerEvents.PickupDestroyed -= OnAnyPickupDestroyed;
        ServerEvents.RoundRestarted -= OnRoundRestarted;
        hooked = false;
    }

    /// <summary>
    /// このシリアルのカスタムアイテムです。無ければ null。
    /// </summary>
    public static CustomItem Of(ushort serial) =>
        BySerial.TryGetValue(serial, out CustomItem item) ? item : null;

    /// <summary>
    /// このシリアルのカスタムアイテムを <typeparamref name="T"/> として返します。
    /// 別の種類か、カスタムアイテムでなければ null。
    /// </summary>
    public static T Of<T>(ushort serial) where T : CustomItem => Of(serial) as T;

    /// <inheritdoc cref="Of(ushort)"/>
    public static CustomItem Of(Item item) => item is null ? null : Of(item.Serial);

    /// <inheritdoc cref="Of{T}(ushort)"/>
    public static T Of<T>(Item item) where T : CustomItem => item is null ? null : Of<T>(item.Serial);

    /// <inheritdoc cref="Of(ushort)"/>
    public static CustomItem Of(Pickup pickup) => pickup is null ? null : Of(pickup.Serial);

    /// <inheritdoc cref="Of{T}(ushort)"/>
    public static T Of<T>(Pickup pickup) where T : CustomItem => pickup is null ? null : Of<T>(pickup.Serial);

    /// <summary>
    /// このシリアルがカスタムアイテムなら、そのインスタンスを返します。
    /// </summary>
    public static bool TryGet(ushort serial, out CustomItem customItem)
    {
        customItem = Of(serial);
        return customItem is not null;
    }

    /// <summary>
    /// このシリアルが <typeparamref name="T"/> のカスタムアイテムなら、
    /// 型付けされたインスタンスを返します。
    /// </summary>
    public static bool TryGet<T>(ushort serial, out T customItem) where T : CustomItem
    {
        customItem = Of<T>(serial);
        return customItem is not null;
    }

    /// <inheritdoc cref="TryGet(ushort, out CustomItem)"/>
    public static bool TryGet(Item item, out CustomItem customItem) =>
        TryGet(item?.Serial ?? 0, out customItem);

    /// <inheritdoc cref="TryGet{T}(ushort, out T)"/>
    public static bool TryGet<T>(Item item, out T customItem) where T : CustomItem =>
        TryGet(item?.Serial ?? 0, out customItem);

    /// <inheritdoc cref="TryGet(ushort, out CustomItem)"/>
    public static bool TryGet(Pickup pickup, out CustomItem customItem) =>
        TryGet(pickup?.Serial ?? 0, out customItem);

    /// <inheritdoc cref="TryGet{T}(ushort, out T)"/>
    public static bool TryGet<T>(Pickup pickup, out T customItem) where T : CustomItem =>
        TryGet(pickup?.Serial ?? 0, out customItem);

    /// <summary>
    /// このシリアルが T のカスタムアイテムかどうか。
    /// </summary>
    public static bool Is<T>(ushort serial) where T : CustomItem => Of(serial) is T;

    /// <summary>
    /// このシリアルが何らかのカスタムアイテムなら、そのインスタンスを返します。
    /// </summary>
    public static bool Is(ushort serial, out CustomItem customItem) => TryGet(serial, out customItem);

    /// <summary>
    /// このシリアルが <typeparamref name="T"/> なら、型付けされたインスタンスを返します。
    /// </summary>
    public static bool Is<T>(ushort serial, out T customItem) where T : CustomItem =>
        TryGet(serial, out customItem);

    /// <summary>
    /// このシリアルが実行時に指定した型なら、そのインスタンスを返します。
    /// 派生型も一致します。
    /// </summary>
    public static bool Is(ushort serial, Type customItemType, out CustomItem customItem)
    {
        customItem = Of(serial);
        return customItemType is not null && customItem is not null && customItemType.IsInstanceOfType(customItem);
    }

    /// <inheritdoc cref="Is(ushort, out CustomItem)"/>
    public static bool Is(Item item, out CustomItem customItem) => TryGet(item, out customItem);

    /// <inheritdoc cref="Is{T}(ushort, out T)"/>
    public static bool Is<T>(Item item, out T customItem) where T : CustomItem => TryGet(item, out customItem);

    /// <inheritdoc cref="Is(ushort, Type, out CustomItem)"/>
    public static bool Is(Item item, Type customItemType, out CustomItem customItem) =>
        Is(item?.Serial ?? 0, customItemType, out customItem);

    /// <inheritdoc cref="Is(ushort, out CustomItem)"/>
    public static bool Is(Pickup pickup, out CustomItem customItem) => TryGet(pickup, out customItem);

    /// <inheritdoc cref="Is{T}(ushort, out T)"/>
    public static bool Is<T>(Pickup pickup, out T customItem) where T : CustomItem => TryGet(pickup, out customItem);

    /// <inheritdoc cref="Is(ushort, Type, out CustomItem)"/>
    public static bool Is(Pickup pickup, Type customItemType, out CustomItem customItem) =>
        Is(pickup?.Serial ?? 0, customItemType, out customItem);

    /// <summary>
    /// プレイヤーに新しいカスタムアイテムを渡します。
    /// </summary>
    public static T Give<T>(Player player) where T : CustomItem, new() => (T)Give(new T(), player);

    /// <summary>
    /// 型を実行時に決めてプレイヤーに渡します。
    /// マップデータや RA コマンドのように、文字列から型を引いてくる経路で使います
    /// (<see cref="TypeParser.TryParse{TBase}"/> と組み合わせる)。
    /// </summary>
    public static CustomItem Give(Type type, Player player) => Give(Instantiate(type), player);

    /// <summary>
    /// 指定した位置に新しいカスタムアイテムを落とします。
    /// </summary>
    public static T Spawn<T>(Vector3 position, Quaternion? rotation = null, Vector3? scale = null)
        where T : CustomItem, new()
        => (T)Spawn(new T(), position, rotation, scale);

    /// <summary>
    /// 型を実行時に決めて、指定した位置に落とします。
    /// </summary>
    public static CustomItem Spawn(Type type, Vector3 position, Quaternion? rotation = null, Vector3? scale = null)
        => Spawn(Instantiate(type), position, rotation, scale);

    /// <summary>
    /// 既に存在するアイテムを、このカスタムアイテムとして追跡し始めます。
    /// SCP-914 での変換や、マップに配置済みのピックアップを引き取るときに使います。
    /// </summary>
    public static T Adopt<T>(ushort serial) where T : CustomItem, new() => (T)Adopt(new T(), serial);

    /// <summary>
    /// 型を実行時に決めて追跡し始めます。
    /// </summary>
    public static CustomItem Adopt(Type type, ushort serial) => Adopt(Instantiate(type), serial);

    /// <summary>
    /// このアイテムの追跡をやめます。以後、イベントは届きません。
    /// </summary>
    public void Release()
    {
        if (trackedSerial == 0) return;

        DetachPickupLight();
        DetachPickupSchematic();
        BySerial.Remove(trackedSerial);
        Invoke(OnTrackingStopped, nameof(OnTrackingStopped));
        trackedSerial = 0;
        hybridContextSerial = 0;
        hybridDestroyAction = null;
        hybridRefreshPickupSchematicAction = null;
    }

    /// <summary>
    /// このアイテムを消します。
    /// </summary>
    public void Destroy()
    {
        ushort serial = Serial;
        Pickup?.Destroy();

        if (Owner is { } owner && ExiledItem.Get(serial) is { } held)
            owner.RemoveItem(held);

        if (trackedSerial != 0)
            Release();
        else
            hybridDestroyAction?.Invoke();
    }

    /// <summary>シリアルへの追跡を開始した直後に呼ばれます。</summary>
    protected virtual void OnTrackingStarted()
    {
    }

    /// <summary>シリアルへの追跡を終了するときに呼ばれます。</summary>
    protected virtual void OnTrackingStopped()
    {
    }

    /// <summary><see cref="Spawn{T}"/> で地面へ生成された直後に呼ばれます。</summary>
    protected virtual void OnPickupSpawned(Pickup pickup)
    {
    }

    /// <summary>追跡中の Pickup 実体が破棄されたときに呼ばれます。</summary>
    protected virtual void OnPickupDestroyed(PickupDestroyedEventArgs ev)
    {
    }

    /// <summary>
    /// 通常アイテムを拾う直前に呼ばれます。<c>ev.IsAllowed = false</c> で拾得を止められます。
    /// </summary>
    protected virtual void OnPickupStarting(PlayerPickingUpItemEventArgs ev)
    {
    }

    /// <summary>アーマーを拾う直前に呼ばれます。</summary>
    protected virtual void OnArmorPickupStarting(PlayerPickingUpArmorEventArgs ev)
    {
    }

    /// <summary>SCP-330 のキャンディを拾う直前に呼ばれます。</summary>
    protected virtual void OnCandyPickupStarting(PlayerPickingUpScp330EventArgs ev)
    {
    }

    /// <summary>
    /// このアイテムの所持者が通常アイテムを拾う直前に呼ばれます。
    /// 所持者のインベントリを拡張するコンテナ系アイテムで使います。
    /// </summary>
    protected virtual void OnOwnerPickupStarting(PlayerPickingUpItemEventArgs ev)
    {
    }

    /// <summary>通常アイテムの拾得が完了したときに呼ばれます。</summary>
    protected virtual void OnPickupCompleted(PlayerPickedUpItemEventArgs ev)
    {
    }

    /// <summary>アーマーの拾得が完了したときに呼ばれます。</summary>
    protected virtual void OnArmorPickupCompleted(PlayerPickedUpArmorEventArgs ev)
    {
    }

    /// <summary>SCP-330 のキャンディの拾得が完了したときに呼ばれます。</summary>
    protected virtual void OnCandyPickupCompleted(PlayerPickedUpScp330EventArgs ev)
    {
    }

    /// <summary>
    /// 落とされる直前に呼ばれます。<c>ev.IsAllowed = false</c> で止められます。
    /// 投げ捨てを射出操作として使うアイテムは <c>ev.Throw</c> を見てください。
    /// </summary>
    protected virtual void OnDropStarting(PlayerDroppingItemEventArgs ev)
    {
    }

    /// <summary>落とし終えたときに呼ばれ、生成された Pickup も参照できます。</summary>
    protected virtual void OnDropCompleted(PlayerDroppedItemEventArgs ev)
    {
    }

    /// <summary>このアイテムへの持ち替えが完了したときに呼ばれます。</summary>
    protected virtual void OnSelected(PlayerChangedItemEventArgs ev)
    {
    }

    /// <summary>このアイテムから別のアイテムへ持ち替えたときに呼ばれます。</summary>
    protected virtual void OnDeselected(PlayerChangedItemEventArgs ev)
    {
    }

    /// <summary>使用モーションを開始する直前に呼ばれます。<c>ev.IsAllowed = false</c> で止められます。</summary>
    protected virtual void OnUseStarting(PlayerUsingItemEventArgs ev)
    {
    }

    /// <summary>
    /// 使用モーションが終わり、バニラの効果が適用される直前に呼ばれます。
    /// <c>ev.IsAllowed = false</c> でバニラの効果を差し替えられます。
    ///
    /// 消費アイテムの効果を自前にしたい場合はここを使ってください。
    /// なお <c>IsAllowed = false</c> にすると <see cref="OnUseCompleted"/> は呼ばれません。
    /// </summary>
    protected virtual void OnUseEffectsApplying(PlayerItemUsageEffectsApplyingEventArgs ev)
    {
    }

    /// <summary>
    /// 使用とバニラ効果の適用が完了したときに呼ばれます。
    /// <see cref="OnUseEffectsApplying"/> で差し止めた場合は呼ばれません。
    /// </summary>
    protected virtual void OnUseCompleted(PlayerUsedItemEventArgs ev)
    {
    }

    /// <summary>使用のキャンセルを要求したときに呼ばれます。</summary>
    protected virtual void OnUseCancelling(PlayerCancellingUsingItemEventArgs ev)
    {
    }

    /// <summary>使用のキャンセルが完了したときに呼ばれます。</summary>
    protected virtual void OnUseCancelled(PlayerCancelledUsingItemEventArgs ev)
    {
    }

    /// <summary>発砲直前に呼ばれます。</summary>
    protected virtual void OnShotStarting(PlayerShootingWeaponEventArgs ev)
    {
    }

    /// <summary>発砲完了時に呼ばれ、発砲イベントの詳細も参照できます。</summary>
    protected virtual void OnShotCompleted(PlayerShotWeaponEventArgs ev)
    {
    }

    /// <summary>空撃ちの直前に呼ばれます。</summary>
    protected virtual void OnDryFireStarting(PlayerDryFiringWeaponEventArgs ev)
    {
    }

    /// <summary>空撃ちが完了したときに呼ばれます。</summary>
    protected virtual void OnDryFireCompleted(PlayerDryFiredWeaponEventArgs ev)
    {
    }

    /// <summary>リロード直前に呼ばれます。</summary>
    protected virtual void OnReloadStarting(PlayerReloadingWeaponEventArgs ev)
    {
    }

    /// <summary>リロード完了時に呼ばれます。</summary>
    protected virtual void OnReloadCompleted(PlayerReloadedWeaponEventArgs ev)
    {
    }

    /// <summary>アタッチメント変更直前に呼ばれます。</summary>
    protected virtual void OnAttachmentsChanging(PlayerChangingAttachmentsEventArgs ev)
    {
    }

    /// <summary>アタッチメント変更完了時に呼ばれます。</summary>
    protected virtual void OnAttachmentsChanged(PlayerChangedAttachmentsEventArgs ev)
    {
    }

    /// <summary>このアイテムを手に持った攻撃者が別プレイヤーを傷つける直前に呼ばれます。</summary>
    protected virtual void OnHurtingPlayer(PlayerHurtingEventArgs ev)
    {
    }

    /// <summary>
    /// このアイテムの所持者が傷つけられる直前に呼ばれます。
    /// アーマーや護符のように、選択中でなくても所持者へ作用するアイテムで使います。
    /// </summary>
    protected virtual void OnOwnerHurting(PlayerHurtingEventArgs ev)
    {
    }

    /// <summary>このアイテムの所持者が死亡する直前に呼ばれます。</summary>
    protected virtual void OnOwnerDying(PlayerDyingEventArgs ev)
    {
    }

    /// <summary>投擲アイテムを投げる直前に呼ばれます。</summary>
    protected virtual void OnProjectileThrowStarting(PlayerThrowingProjectileEventArgs ev)
    {
    }

    /// <summary>投擲アイテムを投げた直後に呼ばれます。</summary>
    protected virtual void OnProjectileThrown(PlayerThrewProjectileEventArgs ev)
    {
    }

    /// <summary>キーカードの表面確認を始める直前に呼ばれます。</summary>
    protected virtual void OnKeycardInspectionStarting(PlayerInspectingKeycardEventArgs ev)
    {
    }

    /// <summary>キーカードの表面確認が完了したときに呼ばれます。</summary>
    protected virtual void OnKeycardInspected(PlayerInspectedKeycardEventArgs ev)
    {
    }

    /// <summary>SCP-914 が地面の Pickup を処理する直前に呼ばれます。</summary>
    protected virtual void OnScp914ProcessingPickup(Scp914ProcessingPickupEventArgs ev)
    {
    }

    /// <summary>SCP-914 がインベントリアイテムを処理する直前に呼ばれます。</summary>
    protected virtual void OnScp914ProcessingInventoryItem(Scp914ProcessingInventoryItemEventArgs ev)
    {
    }

    /// <summary>
    /// Hybrid のモード切替などで、現在の物理アイテムの状態を保存します。
    /// 既定では保存する状態はありません。
    /// </summary>
    protected virtual object OnCaptureState(Item item) => null;

    /// <summary><see cref="OnCaptureState"/> で保存した状態を復元します。</summary>
    protected virtual void OnRestoreState(Item item, object state)
    {
    }

    /// <summary>
    /// Hybrid のモード切替で、このモードが手持ちになった直後に呼ばれます。
    /// </summary>
    protected virtual void OnModeActivated(Item item)
    {
    }

    /// <summary>
    /// インベントリ内の実体に対する見た目・性能の調整です。
    /// </summary>
    protected virtual void Customize(Item item)
    {
    }

    /// <summary>
    /// <see cref="Give{T}(Player)"/> で新規作成したインベントリアイテムを調整します。
    /// 既定では <see cref="Customize(Item)"/> と同じ処理です。
    /// 初期弾数など、新規作成時だけ設定する値がある派生型で上書きします。
    /// </summary>
    protected virtual void CustomizeNewItem(Item item) => Customize(item);

    /// <summary>
    /// 地面のピックアップに対する見た目の調整です。
    /// </summary>
    protected virtual void Customize(Pickup pickup)
    {
    }

    /// <summary>
    /// 地面へ出す Pickup を生成します。派生型は、状態を設定した実アイテムから
    /// Pickup を作る必要がある場合にこのメソッドを上書きします。
    /// </summary>
    /// <remarks>
    /// 戻り値はまだネットワークへ Spawn してはいけません。基底側が
    /// <see cref="Customize(Pickup)"/> の後に一度だけ Spawn します。
    /// </remarks>
    protected virtual Pickup CreatePickup(Vector3 position, Quaternion rotation, Vector3 scale) =>
        Pickup.Create(BaseType, position, rotation, scale, networkSpawn: false);

    /// <summary>
    /// 重ねているスキマティックを今の <see cref="PickupSchematicName"/> で作り直します。
    /// 状態で見た目が変わるアイテムが、状態を変えた後に呼びます。
    /// </summary>
    protected void RefreshPickupSchematic()
    {
        if (trackedSerial == 0 && hybridRefreshPickupSchematicAction != null)
        {
            hybridRefreshPickupSchematicAction();
            return;
        }

        DetachPickupSchematic();
        AttachPickupSchematic();
    }

    private static CustomItem Give(CustomItem custom, Player player)
    {
        if (custom is null || player is null) return null;

        if (player.AddItem(custom.BaseType) is not { } item) return null;

        custom.Attach(item.Serial);

        // AddItem の戻り値が持つ実体を直接橋渡しする。生成直後の serial 検索は
        // LabApi のキャッシュ登録より早いことがあり、その場合だけ初期調整が抜けていた。
        if (Item.Get(item.Base) is not { } wrapper)
        {
            Log.Error($"[Slafight] {custom.GetType().Name} の生成直後のアイテムを取得できませんでした (serial: {item.Serial})。");
            custom.Release();
            player.RemoveItem(item);
            return null;
        }

        custom.ApplyItemCustomization(wrapper, initialize: true);

        return custom;
    }

    private static CustomItem Spawn(CustomItem custom, Vector3 position, Quaternion? rotation, Vector3? scale)
    {
        if (custom is null) return null;

        // networkSpawn: false で作る。
        // Mirror の SpawnMessage はスケールを含むので、Customize で見た目を決めてから Spawn する。
        // 先にネットワーク生成すると UnSpawn → 変更 → Spawn の焼き直しが要り、netId も変わる。
        Pickup pickup = custom.CreatePickup(
            position,
            rotation ?? Quaternion.identity,
            scale ?? Vector3.one);

        if (pickup is null) return null;

        custom.Attach(pickup.Serial);
        custom.ApplyPickupCustomization(pickup);

        pickup.Spawn();
        custom.Invoke(() => custom.OnPickupSpawned(pickup), nameof(OnPickupSpawned));
        custom.AttachPickupLight();
        custom.AttachPickupSchematic();

        return custom;
    }

    private static CustomItem Adopt(CustomItem custom, ushort serial)
    {
        if (custom is null) return null;

        custom.Attach(serial);

        if (custom.Item is { } item)
        {
            custom.ApplyItemCustomization(item, initialize: false);
        }
        else if (custom.Pickup is { } pickup)
        {
            custom.ApplyPickupCustomization(pickup);
            custom.AttachPickupLight();
            custom.AttachPickupSchematic();
        }

        return custom;
    }

    /// <summary>
    /// 型からインスタンスを作ります。カスタムアイテムでない型を渡されたら null を返します。
    /// </summary>
    private static CustomItem Instantiate(Type type)
    {
        if (type is null || !typeof(CustomItem).IsAssignableFrom(type) || type.IsAbstract)
        {
            Log.Error($"[Slafight] {type?.FullName ?? "null"} はカスタムアイテムとして生成できません。");

            return null;
        }

        try
        {
            return (CustomItem)Activator.CreateInstance(type);
        }
        catch (Exception exception)
        {
            Log.Error($"[Slafight] カスタムアイテム {type.FullName} の生成に失敗しました: {exception}");

            return null;
        }
    }

    /// <summary>
    /// LabApi のプレイヤーを EXILED のものへ変換します。
    /// 機能側に LabApi の型を見せないための境界です。
    /// </summary>
    private static Player ToExiled(LabPlayer player) =>
        player?.ReferenceHub is { } hub ? Player.Get(hub) : null;

    public static Color GetRarityColor(Rarity rarity)
    {
        return rarity switch
        {
            Rarity.None => Color.black,
            Rarity.Common => Color.gray,
            Rarity.Uncommon => Color.green,
            Rarity.Rare => Color.cyan,
            Rarity.Epic => Color.magenta,
            Rarity.Legendary => Color.red,
            _ => Color.black
        };
    }

    /// <summary>
    /// 地面のピックアップにライトを付けます。既に付いていれば何もしません。
    /// </summary>
    /// <remarks>
    /// ピックアップの transform にぶら下げるので、投げても転がっても追従します。
    /// <c>AdminToyBase.LateUpdate</c> が毎フレーム位置を同期するため、
    /// 旧実装のような追従コルーチンは要りません。
    ///
    /// <para>
    /// 位置は <b>親からのローカル座標</b>です。<c>AdminToy.Create</c> は
    /// 渡した位置をそのまま <c>localPosition</c> に入れるので、
    /// ここにワールド座標を渡すとライトだけが遠くへ飛びます。
    /// </para>
    /// </remarks>
    private void AttachPickupLight()
    {
        if (!PickupLightEnabled || pickupLight is not null) return;
        if (Pickup is not { } pickup) return;

        LightSourceToy light = LightSourceToy.Create(
            Vector3.zero,
            Quaternion.identity,
            Vector3.one,
            pickup.Transform,
            networkSpawn: false);

        if (light is null) return;

        light.Color = PickupLightColor;
        light.Intensity = PickupLightIntensity;
        light.Range = PickupLightRange;
        light.ShadowType = PickupLightShadowType;
        light.Spawn();

        pickupLight = light;
    }

    /// <summary>ピックアップライトを片付けます。二重に呼んでも安全です。</summary>
    private void DetachPickupLight()
    {
        if (pickupLight is null) return;

        // ピックアップごと消えたときは子のライトも一緒に消えている。
        if (!pickupLight.IsDestroyed)
            pickupLight.Destroy();

        pickupLight = null;
    }

    /// <summary>
    /// 地面のピックアップへスキマティックを重ねます。既に重なっていれば何もしません。
    /// </summary>
    private void AttachPickupSchematic()
    {
        if (pickupSchematic is not null) return;
        if (PickupSchematicName is not { Length: > 0 } model) return;
        if (Pickup is not { } pickup) return;

        SchematicObject schematic = ObjectSpawner.SpawnSchematic(model, pickup.Position, pickup.Transform.rotation);

        if (schematic is null)
        {
            Log.Warn($"[Slafight] {GetType().Name} のスキマティック '{model}' を出せませんでした。");

            return;
        }

        schematic.Scale = PickupSchematicScale;
        schematic.transform.SetParent(pickup.Transform, true);

        pickupSchematic = schematic;
    }

    /// <summary>重ねたスキマティックを片付けます。二重に呼んでも安全です。</summary>
    private void DetachPickupSchematic()
    {
        SchematicObject schematic = pickupSchematic;
        pickupSchematic = null;

        if (schematic == null) return;

        schematic.Destroy();
    }

    /// <summary>拾得・選択のヒントを出します。文言が空なら何もしません。</summary>
    private void ShowItemHint(LabPlayer player, string text, float duration)
    {
        if (ToExiled(player) is not { } target || string.IsNullOrEmpty(text)) return;

        target.ShowHint(text, duration);
    }

    // CustomHybrid がモード定義へ基底フックを中継するための内部 shim。
    internal void SetHybridContext(
        ushort serial,
        Action destroyAction = null,
        Action refreshPickupSchematicAction = null)
    {
        hybridContextSerial = serial;
        hybridDestroyAction = destroyAction;
        hybridRefreshPickupSchematicAction = refreshPickupSchematicAction;
    }

    internal void Rebind(ushort serial)
    {
        if (serial == 0) return;
        if (trackedSerial == serial) return;

        if (trackedSerial != 0)
        {
            DetachPickupLight();
            DetachPickupSchematic();
            BySerial.Remove(trackedSerial);
            trackedSerial = 0;
        }

        Attach(serial);
    }

    internal void CallOnTrackingStarted() => OnTrackingStarted();
    internal void CallOnTrackingStopped() => OnTrackingStopped();
    internal void CallOnPickupSpawned(Pickup pickup) => OnPickupSpawned(pickup);
    internal void CallOnPickupDestroyed(PickupDestroyedEventArgs ev) => OnPickupDestroyed(ev);
    internal void CallOnPickupStarting(PlayerPickingUpItemEventArgs ev) => OnPickupStarting(ev);
    internal void CallOnArmorPickupStarting(PlayerPickingUpArmorEventArgs ev) => OnArmorPickupStarting(ev);
    internal void CallOnCandyPickupStarting(PlayerPickingUpScp330EventArgs ev) => OnCandyPickupStarting(ev);
    internal void CallOnOwnerPickupStarting(PlayerPickingUpItemEventArgs ev) => OnOwnerPickupStarting(ev);
    internal void CallOnPickupCompleted(PlayerPickedUpItemEventArgs ev) => OnPickupCompleted(ev);
    internal void CallOnArmorPickupCompleted(PlayerPickedUpArmorEventArgs ev) => OnArmorPickupCompleted(ev);
    internal void CallOnCandyPickupCompleted(PlayerPickedUpScp330EventArgs ev) => OnCandyPickupCompleted(ev);
    internal void CallOnDropStarting(PlayerDroppingItemEventArgs ev) => OnDropStarting(ev);
    internal void CallOnDropCompleted(PlayerDroppedItemEventArgs ev) => OnDropCompleted(ev);
    internal void CallOnSelected(PlayerChangedItemEventArgs ev) => OnSelected(ev);
    internal void CallOnDeselected(PlayerChangedItemEventArgs ev) => OnDeselected(ev);
    internal void CallOnUseStarting(PlayerUsingItemEventArgs ev) => OnUseStarting(ev);
    internal void CallOnUseEffectsApplying(PlayerItemUsageEffectsApplyingEventArgs ev) => OnUseEffectsApplying(ev);
    internal void CallOnUseCompleted(PlayerUsedItemEventArgs ev) => OnUseCompleted(ev);
    internal void CallOnUseCancelling(PlayerCancellingUsingItemEventArgs ev) => OnUseCancelling(ev);
    internal void CallOnUseCancelled(PlayerCancelledUsingItemEventArgs ev) => OnUseCancelled(ev);
    internal void CallOnShotStarting(PlayerShootingWeaponEventArgs ev) => OnShotStarting(ev);
    internal void CallOnShotCompleted(PlayerShotWeaponEventArgs ev) => OnShotCompleted(ev);
    internal void CallOnDryFireStarting(PlayerDryFiringWeaponEventArgs ev) => OnDryFireStarting(ev);
    internal void CallOnDryFireCompleted(PlayerDryFiredWeaponEventArgs ev) => OnDryFireCompleted(ev);
    internal void CallOnReloadStarting(PlayerReloadingWeaponEventArgs ev) => OnReloadStarting(ev);
    internal void CallOnReloadCompleted(PlayerReloadedWeaponEventArgs ev) => OnReloadCompleted(ev);
    internal void CallOnAttachmentsChanging(PlayerChangingAttachmentsEventArgs ev) => OnAttachmentsChanging(ev);
    internal void CallOnAttachmentsChanged(PlayerChangedAttachmentsEventArgs ev) => OnAttachmentsChanged(ev);
    internal void CallOnHurtingPlayer(PlayerHurtingEventArgs ev) => OnHurtingPlayer(ev);
    internal void CallOnOwnerHurting(PlayerHurtingEventArgs ev) => OnOwnerHurting(ev);
    internal void CallOnOwnerDying(PlayerDyingEventArgs ev) => OnOwnerDying(ev);
    internal void CallOnProjectileThrowStarting(PlayerThrowingProjectileEventArgs ev) => OnProjectileThrowStarting(ev);
    internal void CallOnProjectileThrown(PlayerThrewProjectileEventArgs ev) => OnProjectileThrown(ev);
    internal void CallOnKeycardInspectionStarting(PlayerInspectingKeycardEventArgs ev) => OnKeycardInspectionStarting(ev);
    internal void CallOnKeycardInspected(PlayerInspectedKeycardEventArgs ev) => OnKeycardInspected(ev);
    internal void CallOnScp914ProcessingPickup(Scp914ProcessingPickupEventArgs ev) => OnScp914ProcessingPickup(ev);
    internal void CallOnScp914ProcessingInventoryItem(Scp914ProcessingInventoryItemEventArgs ev) => OnScp914ProcessingInventoryItem(ev);
    internal void CallCustomize(Item item) => Customize(item);
    internal void CallCustomizeNewItem(Item item) => CustomizeNewItem(item);
    internal void CallCustomize(Pickup pickup) => Customize(pickup);
    internal Pickup CallCreatePickup(Vector3 position, Quaternion rotation, Vector3 scale) => CreatePickup(position, rotation, scale);
    internal object CallOnCaptureState(Item item) => OnCaptureState(item);
    internal void CallOnRestoreState(Item item, object state) => OnRestoreState(item, state);
    internal void CallOnModeActivated(Item item) => OnModeActivated(item);
    internal string CallPickupHint() => PickupHint;
    internal string CallSelectedHint() => SelectedHint;
    internal float CallPickupHintDuration() => PickupHintDuration;
    internal float CallSelectedHintDuration() => SelectedHintDuration;
    internal bool CallPickupLightEnabled() => PickupLightEnabled;
    internal Color CallPickupLightColor() => PickupLightColor;
    internal float CallPickupLightIntensity() => PickupLightIntensity;
    internal float CallPickupLightRange() => PickupLightRange;
    internal LightShadows CallPickupLightShadowType() => PickupLightShadowType;
    internal string CallPickupSchematicName() => PickupSchematicName;
    internal Vector3 CallPickupSchematicScale() => PickupSchematicScale;

    private void Attach(ushort serial)
    {
        trackedSerial = serial;

        // 同じシリアルを別のカスタムアイテムが持っていたら明け渡す。
        if (BySerial.TryGetValue(serial, out CustomItem previous) && !ReferenceEquals(previous, this))
            previous.Release();

        BySerial[serial] = this;

        Hook();
        Invoke(OnTrackingStarted, nameof(OnTrackingStarted));
    }

    private void Invoke(Action action, string name)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            Log.Error($"[Slafight] {GetType().Name}.{name} で例外が発生しました: {exception}");
        }
    }

    private void ApplyItemCustomization(Item item, bool initialize)
    {
        try
        {
            if (initialize)
                CustomizeNewItem(item);
            else
                Customize(item);
        }
        catch (Exception exception)
        {
            Log.Error($"[Slafight] {GetType().Name}.{(initialize ? nameof(CustomizeNewItem) : nameof(Customize))} で例外が発生しました: {exception}");
        }
    }

    private void ApplyPickupCustomization(Pickup pickup)
    {
        try
        {
            Customize(pickup);
        }
        catch (Exception exception)
        {
            Log.Error($"[Slafight] {GetType().Name}.{nameof(Customize)}(Pickup) で例外が発生しました: {exception}");
        }
    }

    private void HandlePickedUp(Item item, LabPlayer player, Action completed, string callbackName)
    {
        DetachPickupLight();
        DetachPickupSchematic();
        ApplyItemCustomization(item, initialize: false);
        Invoke(completed, callbackName);
        ShowItemHint(player, PickupHint, PickupHintDuration);
    }

    private static void Hook()
    {
        if (hooked) return;

        hooked = true;

        // 拾得は種類ごとに別イベント。PickedUpItem だけではアーマーとキャンディを取りこぼす。
        PlayerEvents.PickingUpItem += OnAnyPickingUp;
        PlayerEvents.PickingUpArmor += OnAnyPickingUpArmor;
        PlayerEvents.PickingUpScp330 += OnAnyPickingUpScp330;
        PlayerEvents.PickedUpItem += OnAnyPickedUp;
        PlayerEvents.PickedUpArmor += OnAnyPickedUpArmor;
        PlayerEvents.PickedUpScp330 += OnAnyPickedUpScp330;
        PlayerEvents.DroppingItem += OnAnyDropping;
        PlayerEvents.DroppedItem += OnAnyDropped;
        PlayerEvents.ChangedItem += OnAnyChangedItem;
        PlayerEvents.UsingItem += OnAnyUsingItem;
        PlayerEvents.ItemUsageEffectsApplying += OnAnyUsageEffectsApplying;
        PlayerEvents.UsedItem += OnAnyUsedItem;
        PlayerEvents.CancellingUsingItem += OnAnyCancellingUse;
        PlayerEvents.CancelledUsingItem += OnAnyCancelledUse;
        PlayerEvents.ShootingWeapon += OnAnyShootingWeapon;
        PlayerEvents.ShotWeapon += OnAnyShotWeapon;
        PlayerEvents.DryFiringWeapon += OnAnyDryFiringWeapon;
        PlayerEvents.DryFiredWeapon += OnAnyDryFiredWeapon;
        PlayerEvents.ReloadingWeapon += OnAnyReloadingWeapon;
        PlayerEvents.ReloadedWeapon += OnAnyReloadedWeapon;
        PlayerEvents.ChangingAttachments += OnAnyChangingAttachments;
        PlayerEvents.ChangedAttachments += OnAnyChangedAttachments;
        PlayerEvents.Hurting += OnAnyHurting;
        PlayerEvents.Dying += OnAnyDying;
        PlayerEvents.ThrowingProjectile += OnAnyThrowingProjectile;
        PlayerEvents.ThrewProjectile += OnAnyThrewProjectile;
        PlayerEvents.InspectingKeycard += OnAnyInspectingKeycard;
        PlayerEvents.InspectedKeycard += OnAnyInspectedKeycard;
        Scp914Events.ProcessingPickup += OnAnyScp914ProcessingPickup;
        Scp914Events.ProcessingInventoryItem += OnAnyScp914ProcessingInventoryItem;
        ServerEvents.PickupDestroyed += OnAnyPickupDestroyed;
        ServerEvents.RoundRestarted += OnRoundRestarted;
    }

    private static void OnAnyPickingUp(PlayerPickingUpItemEventArgs ev)
    {
        if (Of(ev.Pickup) is { } custom)
            custom.Invoke(() => custom.OnPickupStarting(ev), nameof(OnPickupStarting));

        DispatchOwned(ev.Player,
            item => item.OnOwnerPickupStarting(ev),
            nameof(OnOwnerPickupStarting));
    }

    private static void OnAnyPickingUpArmor(PlayerPickingUpArmorEventArgs ev)
    {
        if (Of(ev.BodyArmorPickup) is { } custom)
            custom.Invoke(() => custom.OnArmorPickupStarting(ev), nameof(OnArmorPickupStarting));
    }

    private static void OnAnyPickingUpScp330(PlayerPickingUpScp330EventArgs ev)
    {
        if (Of(ev.CandyPickup) is { } custom)
            custom.Invoke(() => custom.OnCandyPickupStarting(ev), nameof(OnCandyPickupStarting));
    }

    private static void OnAnyPickedUp(PlayerPickedUpItemEventArgs ev)
    {
        if (Of(ev.Item) is { } custom)
            custom.HandlePickedUp(ev.Item, ev.Player, () => custom.OnPickupCompleted(ev), nameof(OnPickupCompleted));
    }

    // アーマーは PickedUpItem を通らず、専用イベントで来る。
    private static void OnAnyPickedUpArmor(PlayerPickedUpArmorEventArgs ev)
    {
        if (ev.BodyArmorItem is not { } armor) return;

        if (Of(armor.Serial) is { } custom)
            custom.HandlePickedUp(armor, ev.Player, () => custom.OnArmorPickupCompleted(ev), nameof(OnArmorPickupCompleted));
    }

    // SCP-330 のキャンディも専用イベント。
    private static void OnAnyPickedUpScp330(PlayerPickedUpScp330EventArgs ev)
    {
        if (Of(ev.CandyItem.Serial) is { } custom)
            custom.HandlePickedUp(ev.CandyItem, ev.Player, () => custom.OnCandyPickupCompleted(ev), nameof(OnCandyPickupCompleted));
    }

    private static void OnAnyDropping(PlayerDroppingItemEventArgs ev)
    {
        if (Of(ev.Item.Serial) is { } item)
            item.Invoke(() => item.OnDropStarting(ev), nameof(OnDropStarting));
    }

    private static void OnAnyDropped(PlayerDroppedItemEventArgs ev)
    {
        if (Of(ev.Pickup) is { } custom)
        {
            custom.ApplyPickupCustomization(ev.Pickup);
            custom.Invoke(() => custom.OnDropCompleted(ev), nameof(OnDropCompleted));
            custom.AttachPickupLight();
            custom.AttachPickupSchematic();
        }
    }

    private static void OnAnyChangedItem(PlayerChangedItemEventArgs ev)
    {
        if (Of(ev.OldItem) is { } unequipped)
            unequipped.Invoke(() => unequipped.OnDeselected(ev), nameof(OnDeselected));

        if (Of(ev.NewItem) is { } equipped)
        {
            equipped.Invoke(() => equipped.OnSelected(ev), nameof(OnSelected));
            equipped.ShowItemHint(ev.Player, equipped.SelectedHint, equipped.SelectedHintDuration);
        }
    }

    // 以下 4 つの ev.UsableItem / ev.FirearmItem は、そのイベントが発火した時点で
    // 必ず存在する。念のための null チェックは入れない。
    private static void OnAnyUsingItem(PlayerUsingItemEventArgs ev)
    {
        if (Of(ev.UsableItem.Serial) is { } custom)
            custom.Invoke(() => custom.OnUseStarting(ev), nameof(OnUseStarting));
    }

    private static void OnAnyUsageEffectsApplying(PlayerItemUsageEffectsApplyingEventArgs ev)
    {
        if (Of(ev.UsableItem.Serial) is { } custom)
            custom.Invoke(() => custom.OnUseEffectsApplying(ev), nameof(OnUseEffectsApplying));
    }

    private static void OnAnyUsedItem(PlayerUsedItemEventArgs ev)
    {
        if (Of(ev.UsableItem.Serial) is { } custom)
            custom.Invoke(() => custom.OnUseCompleted(ev), nameof(OnUseCompleted));
    }

    private static void OnAnyShootingWeapon(PlayerShootingWeaponEventArgs ev)
    {
        if (Of(ev.FirearmItem.Serial) is { } custom)
            custom.Invoke(() => custom.OnShotStarting(ev), nameof(OnShotStarting));
    }

    private static void OnAnyShotWeapon(PlayerShotWeaponEventArgs ev)
    {
        if (Of(ev.FirearmItem.Serial) is { } custom)
            custom.Invoke(() => custom.OnShotCompleted(ev), nameof(OnShotCompleted));
    }

    private static void OnAnyCancellingUse(PlayerCancellingUsingItemEventArgs ev)
    {
        if (Of(ev.UsableItem.Serial) is { } custom)
            custom.Invoke(() => custom.OnUseCancelling(ev), nameof(OnUseCancelling));
    }

    private static void OnAnyCancelledUse(PlayerCancelledUsingItemEventArgs ev)
    {
        if (Of(ev.UsableItem.Serial) is { } custom)
            custom.Invoke(() => custom.OnUseCancelled(ev), nameof(OnUseCancelled));
    }

    private static void OnAnyDryFiringWeapon(PlayerDryFiringWeaponEventArgs ev)
    {
        if (Of(ev.FirearmItem.Serial) is { } custom)
            custom.Invoke(() => custom.OnDryFireStarting(ev), nameof(OnDryFireStarting));
    }

    private static void OnAnyDryFiredWeapon(PlayerDryFiredWeaponEventArgs ev)
    {
        if (Of(ev.FirearmItem.Serial) is { } custom)
            custom.Invoke(() => custom.OnDryFireCompleted(ev), nameof(OnDryFireCompleted));
    }

    private static void OnAnyReloadingWeapon(PlayerReloadingWeaponEventArgs ev)
    {
        if (Of(ev.FirearmItem.Serial) is { } custom)
            custom.Invoke(() => custom.OnReloadStarting(ev), nameof(OnReloadStarting));
    }

    private static void OnAnyReloadedWeapon(PlayerReloadedWeaponEventArgs ev)
    {
        if (Of(ev.FirearmItem.Serial) is { } custom)
            custom.Invoke(() => custom.OnReloadCompleted(ev), nameof(OnReloadCompleted));
    }

    private static void OnAnyChangingAttachments(PlayerChangingAttachmentsEventArgs ev)
    {
        if (Of(ev.FirearmItem.Serial) is { } custom)
            custom.Invoke(() => custom.OnAttachmentsChanging(ev), nameof(OnAttachmentsChanging));
    }

    private static void OnAnyChangedAttachments(PlayerChangedAttachmentsEventArgs ev)
    {
        if (Of(ev.FirearmItem.Serial) is { } custom)
            custom.Invoke(() => custom.OnAttachmentsChanged(ev), nameof(OnAttachmentsChanged));
    }

    private static void OnAnyHurting(PlayerHurtingEventArgs ev)
    {
        if (ev.Attacker?.CurrentItem is { } item && Of(item.Serial) is { } custom)
            custom.Invoke(() => custom.OnHurtingPlayer(ev), nameof(OnHurtingPlayer));

        DispatchOwned(
            ev.Player,
            owned => owned.OnOwnerHurting(ev),
            nameof(OnOwnerHurting));
    }

    private static void OnAnyDying(PlayerDyingEventArgs ev)
    {
        foreach (Item item in ev.Player.Items)
        {
            if (item != null && Of(item.Serial) is { } custom)
                custom.Invoke(() => custom.OnOwnerDying(ev), nameof(OnOwnerDying));
        }
    }

    private static void OnAnyThrowingProjectile(PlayerThrowingProjectileEventArgs ev)
    {
        if (Of(ev.ThrowableItem.Serial) is { } custom)
            custom.Invoke(() => custom.OnProjectileThrowStarting(ev), nameof(OnProjectileThrowStarting));
    }

    private static void OnAnyThrewProjectile(PlayerThrewProjectileEventArgs ev)
    {
        if (Of(ev.ThrowableItem.Serial) is { } custom)
            custom.Invoke(() => custom.OnProjectileThrown(ev), nameof(OnProjectileThrown));
    }

    private static void OnAnyInspectingKeycard(PlayerInspectingKeycardEventArgs ev)
    {
        if (Of(ev.KeycardItem.Serial) is { } custom)
            custom.Invoke(() => custom.OnKeycardInspectionStarting(ev), nameof(OnKeycardInspectionStarting));
    }

    private static void OnAnyInspectedKeycard(PlayerInspectedKeycardEventArgs ev)
    {
        if (Of(ev.KeycardItem.Serial) is { } custom)
            custom.Invoke(() => custom.OnKeycardInspected(ev), nameof(OnKeycardInspected));
    }

    private static void OnAnyScp914ProcessingPickup(Scp914ProcessingPickupEventArgs ev)
    {
        if (Of(ev.Pickup) is { } custom)
            custom.Invoke(() => custom.OnScp914ProcessingPickup(ev), nameof(OnScp914ProcessingPickup));
    }

    private static void OnAnyScp914ProcessingInventoryItem(Scp914ProcessingInventoryItemEventArgs ev)
    {
        if (Of(ev.Item) is { } custom)
            custom.Invoke(() => custom.OnScp914ProcessingInventoryItem(ev), nameof(OnScp914ProcessingInventoryItem));
    }

    private static void OnAnyPickupDestroyed(PickupDestroyedEventArgs ev)
    {
        if (Of(ev.Pickup) is not { } custom)
            return;

        ushort serial = custom.Serial;
        custom.DetachPickupLight();
        custom.DetachPickupSchematic();
        custom.Invoke(() => custom.OnPickupDestroyed(ev), nameof(OnPickupDestroyed));

        // 拾得遷移でも Pickup は破棄されるため、その場では Release しない。
        // 次フレームにも Item/Pickup のどちらも無ければ、PMER の複数回 Pickup 等で
        // 実体だけ消えた追跡を解放する。
        Timing.CallDelayed(0f, () =>
        {
            if (serial != 0 && ReferenceEquals(Of(serial), custom) && custom.Item == null && custom.Pickup == null)
                custom.Release();
        });
    }

    private static void DispatchOwned(LabPlayer owner, Action<CustomItem> callback, string callbackName)
    {
        if (owner?.ReferenceHub == null)
            return;

        foreach (CustomItem custom in new List<CustomItem>(BySerial.Values))
        {
            if (custom.Owner?.ReferenceHub != owner.ReferenceHub)
                continue;

            custom.Invoke(() => callback(custom), callbackName);
        }
    }

    private static void OnRoundRestarted()
    {
        foreach (CustomItem custom in new List<CustomItem>(BySerial.Values))
        {
            custom.Release();
        }

        BySerial.Clear();
    }
}
