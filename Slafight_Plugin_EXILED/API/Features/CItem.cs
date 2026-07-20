#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using InventorySystem.Items.Usables.Scp1344;
using MEC;
using Mirror;
using PlayerRoles.FirstPersonControl.Thirdperson.Subcontrollers.Wearables;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using UnityEngine;

using PlayerHandlers = Exiled.Events.Handlers.Player;
using MapHandlers = Exiled.Events.Handlers.Map;
using PlayerEvents = Exiled.Events.EventArgs.Player;
using MapEvents = Exiled.Events.EventArgs.Map;
using ServerHandlers = Exiled.Events.Handlers.Server;
using Scp914Handlers = Exiled.Events.Handlers.Scp914;
using Scp914Events = Exiled.Events.EventArgs.Scp914;
using Scp1344Handlers = Exiled.Events.Handlers.Scp1344;
using Scp1344Events = Exiled.Events.EventArgs.Scp1344;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// CRole と同じ思想の、独自で軽量なカスタムアイテム基底クラス。
/// Exiled の CustomItem と異なり、派生クラスは UniqueKey と BaseItem を設定し、
/// 必要なイベントだけを override するだけで動く。
/// シリアル追跡・自動登録・静的イベントディスパッチは基底側で面倒を見る。
/// </summary>
public abstract class CItem
{
    // ======================================================
    // 静的テーブル
    // ======================================================

    // 登録済み全インスタンス
    private static readonly HashSet<CItem> RegisteredInstances = [];

    // 全 CItem 派生具体型（抽象型は除く）
    private static readonly List<Type> ItemTypes;

    // UniqueKey → インスタンス
    private static readonly Dictionary<string, CItem> UniqueKeyToItem =
        new(StringComparer.OrdinalIgnoreCase);

    // 追跡中のシリアル → CItem インスタンス
    // ─ 生存条件: ラウンド内でシリアルが生きている間は残す。
    //   PickupDestroyed 単独では消さない（拾い直しでも発火するため）。
    //   WaitingForPlayers で一括クリアする。
    private static readonly Dictionary<ushort, CItem> SerialToItem = new();

    // Pickup の Light オブジェクト管理
    private static readonly Dictionary<ushort, LabApi.Features.Wrappers.LightSourceToy> PickupLights = new();
    private static readonly Dictionary<ushort, CoroutineHandle> PickupLightCoroutines = new();

    // Pickup に追従する Schematic
    private static readonly Dictionary<ushort, SchematicObject> PickupSchematics = new();
    private static readonly Dictionary<ushort, CoroutineHandle> PickupSchematicCoroutines = new();
    private static readonly HashSet<ushort> ActivePickupAddedDispatches = [];

    private static bool _eventsSubscribed;

    // ======================================================
    // 静的ペンディング状態
    // ゲームロジックはシングルスレッド。
    // Give() / Spawn() が AddItem / CreateAndSpawn を呼んだ瞬間に
    // 同期で ItemAdded / PickupAdded が来るため、
    // 「どの CItem 由来か」を伝えるための一時マーカー。
    // ======================================================

    // Give() 実行中: ItemAdded ハンドラへ「この CItem 由来」を伝える
    private static CItem? _pendingGiveCItem;
    private static bool   _pendingGiveDisplayMessage;

    // Spawn() 実行中: PickupAdded ハンドラへ「この CItem 由来」を伝える
    private static CItem? _pendingSpawnCItem;

    // ======================================================
    // 静的コンストラクタ
    // ======================================================

    static CItem()
    {
        var asm = typeof(CItem).Assembly;
        ItemTypes = asm.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(CItem)) && !t.IsAbstract)
            .ToList();
    }

    // ======================================================
    // 属性
    // ======================================================

    /// <summary>
    /// 自動登録を除外したい CItem 用属性。
    /// 手動で new して OverrideItemInstance で差し替える場合に付ける。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CItemAutoRegisterIgnoreAttribute : Attribute { }

    // ======================================================
    // Plugin 入り口
    // ======================================================

    public static void RegisterAllItems()
    {
        if (!_eventsSubscribed)
        {
            PlayerHandlers.PickingUpItem    += OnAnyPickingUpItem;
            PlayerHandlers.ItemAdded        += OnAnyItemAdded;
            PlayerHandlers.ItemRemoved      += OnAnyItemRemoved;
            PlayerHandlers.DroppingItem     += OnAnyDroppingItem;
            PlayerHandlers.UsingItem        += OnAnyUsingItem;
            PlayerHandlers.UsingItemCompleted += OnAnyUsingItemCompleted;
            PlayerHandlers.UsedItem         += OnAnyUsedItem;
            PlayerHandlers.Shooting         += OnAnyShooting;
            PlayerHandlers.Shot             += OnAnyShot;
            PlayerHandlers.Hurting          += OnAnyHurting;
            PlayerHandlers.Dying            += OnAnyDying;
            PlayerHandlers.ChangingItem     += OnAnyChangingItem;
            PlayerHandlers.ThrowingRequest  += OnAnyThrowingRequest;

            MapHandlers.PickupAdded         += OnAnyPickupAdded;
            MapHandlers.PickupDestroyed     += OnAnyPickupDestroyed;

            // SCP-914 アップグレードを CItem 側でも直接ハンドル。
            // Exiled の CustomKeycard は SCP-914 で正常に処理されない既知バグ
            // (ExMod-Team/EXILED#718) があるため、CItem は独自のシリアル追跡で
            // 同じ挙動を自前実装して回避する。
            Scp914Handlers.UpgradingPickup          += OnAnyUpgradingPickup;
            Scp914Handlers.UpgradingInventoryItem   += OnAnyUpgradingInventoryItem;

            // Goggles (Scp1344) 装着・取り外しライフサイクル
            Scp1344Handlers.ChangedStatus  += OnAnyScp1344ChangedStatus;
            Scp1344Handlers.ChangingStatus += OnAnyScp1344ChangingStatus;
            Scp1344Handlers.Deactivating   += OnAnyScp1344Deactivating;

            ServerHandlers.WaitingForPlayers += OnAnyWaitingForPlayers;

            _eventsSubscribed = true;
        }

        foreach (var type in ItemTypes)
        {
            try
            {
                var instance = (CItem)Activator.CreateInstance(type);

                if (string.IsNullOrEmpty(instance.UniqueKey))
                {
                    Log.Warn($"CItem.RegisterAllItems: {type.Name} has null/empty UniqueKey, skipping");
                    continue;
                }

                if (RegisteredInstances.Any(item => item.GetType() == type) ||
                    UniqueKeyToItem.ContainsKey(instance.UniqueKey))
                {
                    Log.Warn($"CItem.RegisterAllItems: {type.Name} key={instance.UniqueKey} is already registered, skipping duplicate");
                    continue;
                }

                bool autoRegisterEvents =
                    type.GetCustomAttributes(typeof(CItemAutoRegisterIgnoreAttribute), true).Length == 0;

                instance.InternalRegister(autoRegisterEvents);
            }
            catch (Exception ex)
            {
                Log.Error($"CItem.RegisterAllItems failed for {type.Name}: {ex}");
            }
        }
    }

    public static void UnregisterAllItems()
    {
        foreach (var instance in RegisteredInstances.ToList())
            instance.InternalUnregister();

        RegisteredInstances.Clear();
        UniqueKeyToItem.Clear();
        SerialToItem.Clear();
        ActivePickupAddedDispatches.Clear();
        _pendingGiveCItem = null;
        _pendingGiveDisplayMessage = false;
        _pendingSpawnCItem = null;

        foreach (var serial in PickupLights.Keys.ToList())
            DestroyPickupLightInternal(serial);
        PickupLights.Clear();
        PickupLightCoroutines.Clear();

        foreach (var serial in PickupSchematics.Keys.ToList())
            DestroyPickupSchematicInternal(serial);
        PickupSchematics.Clear();
        PickupSchematicCoroutines.Clear();

        if (_eventsSubscribed)
        {
            PlayerHandlers.PickingUpItem    -= OnAnyPickingUpItem;
            PlayerHandlers.ItemAdded        -= OnAnyItemAdded;
            PlayerHandlers.ItemRemoved      -= OnAnyItemRemoved;
            PlayerHandlers.DroppingItem     -= OnAnyDroppingItem;
            PlayerHandlers.UsingItem        -= OnAnyUsingItem;
            PlayerHandlers.UsingItemCompleted -= OnAnyUsingItemCompleted;
            PlayerHandlers.UsedItem         -= OnAnyUsedItem;
            PlayerHandlers.Shooting         -= OnAnyShooting;
            PlayerHandlers.Shot             -= OnAnyShot;
            PlayerHandlers.Hurting          -= OnAnyHurting;
            PlayerHandlers.Dying            -= OnAnyDying;
            PlayerHandlers.ChangingItem     -= OnAnyChangingItem;
            PlayerHandlers.ThrowingRequest  -= OnAnyThrowingRequest;

            MapHandlers.PickupAdded         -= OnAnyPickupAdded;
            MapHandlers.PickupDestroyed     -= OnAnyPickupDestroyed;

            Scp914Handlers.UpgradingPickup          -= OnAnyUpgradingPickup;
            Scp914Handlers.UpgradingInventoryItem   -= OnAnyUpgradingInventoryItem;

            Scp1344Handlers.ChangedStatus  -= OnAnyScp1344ChangedStatus;
            Scp1344Handlers.ChangingStatus -= OnAnyScp1344ChangingStatus;
            Scp1344Handlers.Deactivating   -= OnAnyScp1344Deactivating;

            ServerHandlers.WaitingForPlayers -= OnAnyWaitingForPlayers;

            _eventsSubscribed = false;
        }
    }

    /// <summary>
    /// CItemAutoRegisterIgnore 付き CItem 用:
    /// 手動で生成したインスタンスを UniqueKey マップの「本体」として登録する。
    /// </summary>
    public static void OverrideItemInstance(string uniqueKey, CItem? instance)
    {
        if (string.IsNullOrEmpty(uniqueKey) || instance == null) return;
        UniqueKeyToItem[uniqueKey] = instance;
        Log.Debug($"CItem.OverrideItemInstance: {uniqueKey} -> {instance.GetType().Name}");
    }

    /// <summary>登録済みすべての CItem インスタンスを返す（読み取り専用）。</summary>
    public static IReadOnlyCollection<CItem> GetAllInstances()
        => RegisteredInstances;

    // ======================================================
    // 派生クラスが実装するメタ情報
    // ======================================================

    protected abstract string   UniqueKey { get; }
    protected abstract ItemType BaseItem  { get; }

    public virtual string DisplayName => UniqueKey;
    public virtual string Description => string.Empty;
    public string UniqueKeyName => UniqueKey;

    // ======================================================
    // Hint 設定
    // ======================================================

    private const string PickedUpHintFormat   = "<size=24>あなたは{0}を拾いました！\n{1}</size>";
    private const float  PickedUpHintDuration = 6f;

    private const string SelectedHintFormat   = "<size=24>あなたは{0}を選択しました！\n{1}</size>";
    private const float  SelectedHintDuration = 5f;

    protected virtual bool ShowPickedUpHint => true;
    protected virtual bool ShowSelectedHint => true;

    // ======================================================
    // Pickup ライト設定
    // ======================================================

    protected virtual bool         PickupLightEnabled    => false;
    protected virtual Color        PickupLightColor      => Color.white;
    protected virtual float        PickupLightIntensity  => 0.7f;
    protected virtual float        PickupLightRange      => 3.75f;
    protected virtual LightShadows PickupLightShadowType => LightShadows.None;

    // ======================================================
    // Pickup Schematic 設定
    // ======================================================

    protected virtual string? PickupSchematicName => null;
    protected virtual Vector3 PickupSchematicOffset => Vector3.zero;
    protected virtual Vector3 PickupSchematicRotationOffset => Vector3.zero;
    protected virtual Vector3 PickupSchematicScale => Vector3.one;
    protected virtual string? ResolvePickupSchematicName(Pickup pickup) => PickupSchematicName;

    // ======================================================
    // Goggles (Scp1344) 設定
    // ======================================================

    protected virtual bool  IsGoggles         => false;
    protected virtual float WearingTime       => 5f;
    protected virtual float RemovingTime      => 5.1f;
    protected virtual bool  Remove1344Effect  => true;
    protected virtual bool  CanBeRemoveSafely => true;

    protected virtual void OnGogglesWorn   (Player player, Scp1344 goggles) { }
    protected virtual void OnGogglesRemoved(Player player, Scp1344 goggles) { }

    // ======================================================
    // Hint メッセージ
    // ======================================================

    protected virtual string BuildPickedUpMessage()
        => string.Format(PickedUpHintFormat, DisplayName, Description);

    protected virtual string BuildSelectedMessage()
        => string.Format(SelectedHintFormat, DisplayName, Description);

    protected virtual void ShowPickedUpMessage(Player player)
    {
        if (player == null) return;
            player.ShowHint(BuildPickedUpMessage(), PickedUpHintDuration);
        var captured = player;
        Timing.CallDelayed(PickedUpHintDuration, () =>
        {
            try { OnPickedUpHintFinished(captured); }
            catch (Exception e) { Log.Error($"CItem.OnPickedUpHintFinished error in {GetType().Name}: {e}"); }
        });
    }

    protected virtual void ShowSelectedMessage(Player player)
    {
        if (player == null) return;
            player.ShowHint(BuildSelectedMessage(), SelectedHintDuration);
        var captured = player;
        Timing.CallDelayed(SelectedHintDuration, () =>
        {
            try { OnSelectedHintFinished(captured); }
            catch (Exception e) { Log.Error($"CItem.OnSelectedHintFinished error in {GetType().Name}: {e}"); }
        });
    }

    protected virtual void OnPickedUpHintFinished(Player player) { }
    protected virtual void OnSelectedHintFinished (Player player) { }

    // ======================================================
    // インスタンス管理
    // ======================================================

    private void InternalRegister(bool autoRegisterEvents)
    {
        if (!RegisteredInstances.Add(this)) return;

        UniqueKeyToItem[UniqueKey] = this;

        if (autoRegisterEvents)
            RegisterEvents();

        Log.Debug($"CItem registered: {GetType().Name} key={UniqueKey} (autoEvents={autoRegisterEvents})");
    }

    private void InternalUnregister()
    {
        if (!RegisteredInstances.Remove(this)) return;

        if (UniqueKeyToItem.TryGetValue(UniqueKey, out var inst) && ReferenceEquals(inst, this))
            UniqueKeyToItem.Remove(UniqueKey);

        var mine = SerialToItem.Where(kv => ReferenceEquals(kv.Value, this))
                               .Select(kv => kv.Key)
                               .ToList();
        foreach (var s in mine) SerialToItem.Remove(s);

        UnregisterEvents();
        Log.Debug($"CItem unregistered: {GetType().Name}");
    }

    public virtual void RegisterEvents()   { }
    public virtual void UnregisterEvents() { }

    // ======================================================
    // 付与 / 生成
    // ======================================================

    /// <summary>
/// プレイヤーにこの CItem を付与する。
/// AddItem の戻り値を正とし、その場で SerialToItem 登録を行う。
/// ItemAdded は補助として扱い、必須依存しない。
/// </summary>
public virtual Item? Give(Player? player, bool displayMessage = false)
{
    if (player == null) return null;

    Item? item = null;

    try
    {
        // ここでは pending は「誰が呼んだか」をイベント側に伝える用途だけに絞る。
        _pendingGiveCItem          = this;
        _pendingGiveDisplayMessage = displayMessage;

        item = player.AddItem(BaseItem);
        if (item == null) return null;

        // AddItem のイベント順序に依存せず、戻り値で即登録する。
        SerialToItem[item.Serial] = this;

        // ここでの登録が真実なので、ItemAdded で二重登録されても問題ない設計にする。
        CustomizeItem(item);
        return item;
    }
    catch (Exception ex)
    {
        CleanupFailedGivenItem(player, item);
        Log.Error($"CItem.Give failed ({GetType().Name}): {ex}");
        return null;
    }
    finally
    {
        // pending は「使えたらラッキー」程度の補助情報なので必ずクリアする。
        _pendingGiveCItem          = null;
        _pendingGiveDisplayMessage = false;
    }
}

/// <summary>指定位置にこの CItem の Pickup を生成する（安全版）。</summary>
public virtual Pickup? Spawn(Vector3 position)
{
    Pickup? pickup = null;

    try
    {
        _pendingSpawnCItem = this;

        pickup = CreatePickupForSpawn(position);
        if (pickup == null) return null;

        // CreatePickupForSpawn のイベント順序に依存せず、戻り値で即登録。
        if (SerialToItem.TryGetValue(pickup.Serial, out var tracked) &&
            tracked != null &&
            !ReferenceEquals(tracked, this))
        {
            // ここは情報として Debug に落とすだけにする。Warn スパム防止。
            Log.Debug($"[CItem] Spawn: serial={pickup.Serial} was tracked by {tracked.GetType().Name}, overriding with {GetType().Name}.");
        }

        SerialToItem[pickup.Serial] = this;

        // PickupAdded を取りこぼしても最低限の初期化はここで完了させる。
        CustomizePickup(pickup);
        OnSpawned(pickup);

        // PickupAdded 側でライトや Schematic を張るためのフォールバック。
        DispatchPickupAddedFallback(this, pickup);

        return pickup;
    }
    catch (Exception ex)
    {
        CleanupFailedSpawnedPickup(pickup);
        Log.Error($"CItem.Spawn failed ({GetType().Name}): {ex}");
        return null;
    }
    finally
    {
        _pendingSpawnCItem = null;
    }
}

    /// <summary>
    /// Pickup の生成方法を差し替えたい場合のフック。
    /// デフォルトは Pickup.CreateAndSpawn。
    /// </summary>
    protected virtual Pickup? CreatePickupForSpawn(Vector3 position)
        => Pickup.CreateAndSpawn(BaseItem, position, Quaternion.identity);

    /// <summary>
    /// Give / Spawn 後にアイテムへ追加カスタマイズを適用するフック。
    /// 派生クラスは必ず base.CustomizeItem(item) を呼ぶこと。
    /// Armor 固有ロジック（VestEfficacy 等）は <see cref="CItemArmor"/> 側に移管済み。
    /// </summary>
    protected virtual void CustomizeItem(Item item) { }

    /// <summary>
    /// Spawn 後に Pickup へ追加カスタマイズを適用するフック。
    /// Pickup の生成方法そのものを変えたい場合は <see cref="CreatePickupForSpawn"/> を override する。
    /// </summary>
    protected virtual void CustomizePickup(Pickup pickup) { }

    /// <summary>
    /// SerialToItem から tracking が外れる直前の通知。
    /// CItemHybrid の mode map など、派生クラス側の serial 状態を同時に掃除するために使う。
    /// </summary>
    protected virtual void OnSerialUntracked(ushort serial) { }

    // ======================================================
    // 状態キャプチャ / 復元プロトコル
    // ======================================================

    /// <summary>
    /// このアイテムの per-item 状態をスナップショットとして取り出す。
    /// CItemHybrid のモード切替で、破棄される旧アイテムの状態（弾数など）を
    /// 保存しておくために使う。武器以外の Hybrid でも任意の状態を保存できる。
    /// デフォルトは状態無し（null）。
    /// </summary>
    protected virtual object? OnCaptureState(Item item) => null;

    /// <summary>
    /// <see cref="OnCaptureState"/> で取り出した状態をアイテムへ書き戻す。
    /// state が null や型違いの場合は何もしない。
    /// </summary>
    protected virtual void OnRestoreState(Item item, object? state) { }

    /// <summary>
    /// CItemHybrid のモード切替でこのアイテムがアクティブ（手持ち）になった直後に呼ばれる。
    /// 強制持ち替え後のレディ化（銃の cock など）に使う。
    /// 通常の Give（プレイヤーが自然に持ち替える）経路では呼ばれない。
    /// </summary>
    protected virtual void OnModeActivated(Item item) { }

    private static void UntrackSerial(ushort serial)
    {
        if (SerialToItem.TryGetValue(serial, out var ci) && ci != null)
        {
            try { ci.OnSerialUntracked(serial); }
            catch (Exception ex) { Log.Warn($"CItem.OnSerialUntracked error in {ci.GetType().Name}: {ex}"); }
        }

        SerialToItem.Remove(serial);
    }

    private static void CleanupFailedGivenItem(Player? player, Item? item)
    {
        if (item == null) return;

        UntrackSerial(item.Serial);

        try
        {
            if (player?.Items.Any(i => i?.Serial == item.Serial) == true)
                player.RemoveItem(item, destroy: true);
            else
                item.Destroy();
        }
        catch (Exception ex)
        {
            Log.Warn($"CItem.CleanupFailedGivenItem failed for serial={item.Serial}: {ex}");
        }
    }

    private static void CleanupFailedSpawnedPickup(Pickup? pickup)
    {
        if (pickup == null) return;

        var serial = pickup.Serial;
        UntrackSerial(serial);
        ActivePickupAddedDispatches.Remove(serial);
        DestroyPickupLightInternal(serial);
        DestroyPickupSchematicInternal(serial);

        try
        {
            pickup.Destroy();
        }
        catch (Exception ex)
        {
            Log.Warn($"CItem.CleanupFailedSpawnedPickup failed for serial={serial}: {ex}");
        }
    }

    // ======================================================
    // Pickup ライト制御
    // ======================================================

    public virtual LabApi.Features.Wrappers.LightSourceToy? AddPickupLight(Pickup? pickup)
    {
        if (pickup == null) return null;
        if (PickupLights.TryGetValue(pickup.Serial, out var existing)) return existing;

        try
        {
            var light = LabApi.Features.Wrappers.LightSourceToy.Create(pickup.Position);
            if (light == null) return null;

            light.Color      = PickupLightColor;
            light.Intensity  = PickupLightIntensity;
            light.Range      = PickupLightRange;
            light.ShadowType = PickupLightShadowType;

            var serial = pickup.Serial;
            PickupLights[serial]          = light;
            PickupLightCoroutines[serial] = Timing.RunCoroutine(TrackPickupLight(serial, pickup, light));

            Log.Debug($"[CItem] AddPickupLight: serial={serial} ci={GetType().Name}");

            return light;
        }
        catch (Exception ex)
        {
            Log.Error($"CItem.AddPickupLight failed ({GetType().Name}): {ex}");
            return null;
        }
    }

    private static IEnumerator<float> TrackPickupLight(
        ushort serial, Pickup? pickup, LabApi.Features.Wrappers.LightSourceToy? light)
    {
        while (pickup?.Base?.gameObject != null && light?.Base?.gameObject != null)
        {
            light.Position = pickup.Position;
            yield return Timing.WaitForOneFrame;
        }

        // PickupDestroyed を取りこぼした場合のフォールバック。
        // pickup の GameObject が消えたのに Light だけ取り残されるケースを防ぐ。
        // 既に正規経路で破棄済みなら PickupLights に存在しないため何もしない（冪等）。
        if (PickupLights.ContainsKey(serial))
        {
            Log.Debug($"[CItem] TrackPickupLight ended without PickupDestroyed cleanup: serial={serial}. Forcing light removal.");
            DestroyPickupLightInternal(serial);
        }
    }

    public virtual bool RemovePickupLight(Pickup? pickup)
    {
        if (pickup == null) return false;
        return DestroyPickupLightInternal(pickup.Serial);
    }

    private static bool DestroyPickupLightInternal(ushort serial)
    {
        if (PickupLightCoroutines.TryGetValue(serial, out var handle))
        {
            Timing.KillCoroutines(handle);
            PickupLightCoroutines.Remove(serial);
        }

        if (!PickupLights.TryGetValue(serial, out var light)) return false;

        try
        {
            if (light?.Base?.gameObject != null)
                NetworkServer.Destroy(light.Base.gameObject);
        }
        catch (Exception ex)
        {
            Log.Error($"CItem.DestroyPickupLightInternal failed: {ex}");
        }

        return PickupLights.Remove(serial);
    }

    // ======================================================
    // Pickup Schematic 追従
    // ======================================================

    private static void AttachPickupSchematic(
        Pickup pickup,
        string schematicName,
        Vector3 offset,
        Vector3 rotationOffset,
        Vector3 scale,
        int attempt = 0)
    {
        if (pickup?.Base?.gameObject == null || PickupSchematics.ContainsKey(pickup.Serial)) return;

        try
        {
            var rotation = pickup.Rotation * Quaternion.Euler(rotationOffset);
            var schem = ObjectSpawner.SpawnSchematic(
                schematicName,
                pickup.Position + pickup.Rotation * offset,
                rotation,
                scale);
            if (schem == null)
            {
                RetryAttachPickupSchematic(pickup, schematicName, offset, rotationOffset, scale, attempt);
                return;
            }

            PickupSchematics[pickup.Serial]          = schem;
            PickupSchematicCoroutines[pickup.Serial] = Timing.RunCoroutine(
                TrackPickupSchematic(pickup, schem, offset, rotationOffset));
        }
        catch (Exception ex)
        {
            if (attempt >= 2)
                Log.Error($"CItem.AttachPickupSchematic({schematicName}) failed after {attempt + 1} attempts: {ex}");
            else
                RetryAttachPickupSchematic(pickup, schematicName, offset, rotationOffset, scale, attempt);
        }
    }

    private static void RetryAttachPickupSchematic(
        Pickup pickup,
        string schematicName,
        Vector3 offset,
        Vector3 rotationOffset,
        Vector3 scale,
        int attempt)
    {
        if (attempt >= 2)
        {
            Log.Warn($"CItem.AttachPickupSchematic({schematicName}) returned null after {attempt + 1} attempts.");
            return;
        }

        Timing.CallDelayed(0.25f, () =>
            AttachPickupSchematic(pickup, schematicName, offset, rotationOffset, scale, attempt + 1));
    }

    private static IEnumerator<float> TrackPickupSchematic(
        Pickup? pickup,
        SchematicObject? schem,
        Vector3 offset,
        Vector3 rotationOffset)
    {
        while (pickup?.Base?.gameObject != null && schem?.gameObject != null)
        {
            schem.transform.position = pickup.Position + pickup.Rotation * offset;
            schem.transform.rotation = pickup.Rotation * Quaternion.Euler(rotationOffset);
            yield return Timing.WaitForOneFrame;
        }
    }

    private static void DestroyPickupSchematicInternal(ushort serial)
    {
        if (PickupSchematicCoroutines.TryGetValue(serial, out var handle))
        {
            Timing.KillCoroutines(handle);
            PickupSchematicCoroutines.Remove(serial);
        }

        if (!PickupSchematics.TryGetValue(serial, out var schem)) return;

        try { schem?.Destroy(); }
        catch (Exception ex) { Log.Warn($"CItem.DestroyPickupSchematicInternal failed: {ex}"); }

        PickupSchematics.Remove(serial);
    }

    // ======================================================
    // プレイヤー所持確認 / 削除ヘルパー
    // ======================================================

    public static void RebuildHybridStateFor(Player player)
    {
        if (player == null) return;
        foreach (var item in player.Items)
        {
            if (item == null) continue;
            if (!TryGet(item, out var ci) || ci is not CItemHybrid hybrid) continue;
            hybrid.RebindSerialFor(item, player);
        }
    }

    public bool RemoveFrom(Player? player, bool destroy = true)
    {
        if (player == null) return false;

        foreach (var item in player.Items.ToList())
        {
            if (item == null || !Check(item)) continue;

            UntrackSerial(item.Serial);
            if (destroy) player.RemoveItem(item, destroy: true);
            return true;
        }
        return false;
    }

    // ======================================================
    // Check / TryGet
    // ======================================================

    /// <summary>
    /// このアイテムがこの CItem インスタンス由来か確認する。
    /// Hybrid の sub インスタンスから呼んだ場合、その serial の現在アクティブな
    /// sub が自分なら true を返す。
    /// </summary>
    public bool Check(Item? item)
    {
        if (item == null) return false;
        if (!SerialToItem.TryGetValue(item.Serial, out var ci) || ci == null) return false;
        if (ReferenceEquals(ci, this)) return true;
        return ci is CItemHybrid hybrid && hybrid.IsCurrentSub(item.Serial, this);
    }

    public bool Check(Pickup? pickup)
    {
        if (pickup == null) return false;
        if (!SerialToItem.TryGetValue(pickup.Serial, out var ci) || ci == null) return false;
        if (ReferenceEquals(ci, this)) return true;
        return ci is CItemHybrid hybrid && hybrid.IsCurrentSub(pickup.Serial, this);
    }

    public bool CheckHeld(Player? player) => player != null && Check(player.CurrentItem);

    public bool HasIn(Player? player)
    {
        if (player == null) return false;
        foreach (var it in player.Items)
            if (Check(it)) return true;
        return false;
    }

    public static bool TryGet(ushort serial, out CItem? cItem)
        => SerialToItem.TryGetValue(serial, out cItem!);

    public static bool TryGet(Item? item, out CItem? cItem)
    {
        cItem = null;
        return item != null && SerialToItem.TryGetValue(item.Serial, out cItem!);
    }

    public static bool TryGet(Pickup? pickup, out CItem? cItem)
    {
        cItem = null;
        return pickup != null && SerialToItem.TryGetValue(pickup.Serial, out cItem!);
    }

    public static bool TryGetByKey(string uniqueKey, out CItem? cItem)
        => UniqueKeyToItem.TryGetValue(uniqueKey, out cItem!);

    /// <summary>
    /// UniqueKey（大文字小文字無視）を優先し、見つからなければ
    /// クラス名の一意一致で CItem を解決する。ItemSpawnpoint の統一 Item 指定などに使う。
    /// </summary>
    public static bool TryResolve(string nameOrKey, out CItem? cItem)
    {
        cItem = null;
        if (string.IsNullOrWhiteSpace(nameOrKey))
            return false;

        string trimmed = nameOrKey.Trim();
        if (TryGetByKey(trimmed, out cItem) && cItem != null)
            return true;

        var matches = RegisteredInstances
            .Where(item => string.Equals(item.GetType().Name, trimmed, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 1)
        {
            cItem = matches[0];
            return true;
        }

        cItem = null;
        return false;
    }

    public static T? Get<T>() where T : CItem
        => RegisteredInstances.OfType<T>().FirstOrDefault();

    // ======================================================
    // 派生クラス向けイベントフック（virtual、デフォルト空実装）
    // ======================================================

    /// <summary>
    /// インベントリに入った瞬間（経路問わず）。
    /// Give / 床からのピックアップ / ロードアウト / SCP-914 / 他プラグイン AddItem すべて。
    /// </summary>
    protected virtual void OnAcquired(PlayerEvents.ItemAddedEventArgs ev, bool displayMessage) { }
    protected virtual void OnReleased(PlayerEvents.ItemRemovedEventArgs ev) { }
    protected virtual void OnSpawned (Pickup pickup) { }

    protected virtual void OnPickingUp  (PlayerEvents.PickingUpItemEventArgs  ev) { }
    protected virtual void OnDropping   (PlayerEvents.DroppingItemEventArgs   ev) { }
    protected virtual void OnUsing      (PlayerEvents.UsingItemEventArgs      ev) { }
    protected virtual void OnUsingItemCompleted(PlayerEvents.UsingItemCompletedEventArgs ev) { }
    protected virtual void OnUsed       (PlayerEvents.UsedItemEventArgs       ev) { }
    protected virtual void OnShooting   (PlayerEvents.ShootingEventArgs       ev) { }
    protected virtual void OnShot       (PlayerEvents.ShotEventArgs           ev) { }
    protected virtual void OnHurtingOthers(PlayerEvents.HurtingEventArgs      ev) { }
    protected virtual void OnOwnerDying (PlayerEvents.DyingEventArgs          ev) { }
    protected virtual void OnChangingItem(PlayerEvents.ChangingItemEventArgs   ev) { }
    protected virtual void OnThrowingRequest(PlayerEvents.ThrowingRequestEventArgs ev) { }
    protected virtual void OnPickupAdded    (MapEvents.PickupAddedEventArgs   ev) { }
    protected virtual void OnPickupDestroyed(MapEvents.PickupDestroyedEventArgs ev) { }
    protected virtual void OnWaitingForPlayers() { }

    /// <summary>
    /// SCP-914 が床の Pickup をアップグレードするとき。
    /// デフォルト: キャンセルして出力位置にテレポート（Keep 相当）。
    /// </summary>
    protected virtual void OnUpgradingPickup(Scp914Events.UpgradingPickupEventArgs ev)
    {
        ev.IsAllowed = false;
        if (ev.Pickup != null) ev.Pickup.Position = ev.OutputPosition;
    }

    /// <summary>
    /// SCP-914 がインベントリ内のアイテムをアップグレードするとき。
    /// デフォルト: キャンセルして CItem を保持。
    /// </summary>
    protected virtual void OnUpgradingInventoryItem(Scp914Events.UpgradingInventoryItemEventArgs ev)
    {
        ev.IsAllowed = false;
    }

    // ======================================================
    // 静的イベントハンドラ（全アイテム共通ディスパッチ）
    // ======================================================

    private static void Dispatch(ushort serial, Action<CItem> body, string tag)
    {
        if (!SerialToItem.TryGetValue(serial, out var ci) || ci == null) return;
        try { body(ci); }
        catch (Exception ex) { Log.Error($"CItem.{tag} error in {ci.GetType().Name}: {ex}"); }
    }

    private static void OnAnyPickingUpItem(PlayerEvents.PickingUpItemEventArgs? ev)
    {
        if (ev?.Pickup == null) return;
        Dispatch(ev.Pickup.Serial, ci => ci.OnPickingUp(ev), nameof(OnPickingUp));
    }

    /// <summary>
    /// EXILED の ItemAdded。戻り値で既に SerialToItem 登録されている前提で、
    /// 「誰由来か分からない場合のみ pending 情報を使う」ように変更。
    /// </summary>
    private static void OnAnyItemAdded(PlayerEvents.ItemAddedEventArgs? ev)
    {
        if (ev?.Player == null || ev.Item == null) return;

        CItem? ci;
        bool   displayMessage;

        // すでに Give/Spawn 側で SerialToItem 登録済みなら、それを優先する。
        if (SerialToItem.TryGetValue(ev.Item.Serial, out var existing) && existing != null)
        {
            ci             = existing;
            displayMessage = true;

            Log.Debug($"[CItem] ItemAdded(tracked): serial={ev.Item.Serial} ci={ci.GetType().Name}");
        }
        else if (_pendingGiveCItem != null)
        {
            // ここはあくまで「補助経路」。miss しても致命傷ではない。
            ci             = _pendingGiveCItem;
            displayMessage = _pendingGiveDisplayMessage;

            _pendingGiveCItem          = null;
            _pendingGiveDisplayMessage = false;

            SerialToItem[ev.Item.Serial] = ci;

            Log.Debug($"[CItem] ItemAdded(give-pending): serial={ev.Item.Serial} ci={ci.GetType().Name}");
        }
        else
        {
            // 追跡対象外。以前の warn / debug スパムは削る。
            Log.Debug($"[CItem] ItemAdded(unknown): serial={ev.Item.Serial}");
            return;
        }

        try
        {
            ci.OnAcquired(ev, displayMessage);
        }
        catch (Exception e)
        {
            Log.Error($"CItem.OnAcquired error in {ci.GetType().Name}: {e}");
        }

        if (displayMessage && ci.ShowPickedUpHint)
        {
            try
            {
                ci.ShowPickedUpMessage(ev.Player);
            }
            catch (Exception e)
            {
                Log.Error($"CItem.ShowPickedUpMessage error in {ci.GetType().Name}: {e}");
            }
        }
    }

    private static void OnAnyItemRemoved(PlayerEvents.ItemRemovedEventArgs? ev)
    {
        if (ev?.Item == null) return;
        // ItemRemoved は「インベントリから抜けた」通知。
        // 対応 Pickup への遷移中の可能性があるため SerialToItem は削除しない。
        // 本当の消滅は WaitingForPlayers で一括クリアする。
        if (!SerialToItem.TryGetValue(ev.Item.Serial, out var ci) || ci == null) return;
        try { ci.OnReleased(ev); }
        catch (Exception e) { Log.Error($"CItem.OnReleased error in {ci.GetType().Name}: {e}"); }
    }

    private static void OnAnyDroppingItem(PlayerEvents.DroppingItemEventArgs? ev)
    {
        if (ev?.Item == null) return;
        Dispatch(ev.Item.Serial, ci => ci.OnDropping(ev), nameof(OnDropping));
    }

    private static void OnAnyUsingItem(PlayerEvents.UsingItemEventArgs? ev)
    {
        if (ev?.Item == null) return;
        Dispatch(ev.Item.Serial, ci => ci.OnUsing(ev), nameof(OnUsing));
    }

    private static void OnAnyUsingItemCompleted(PlayerEvents.UsingItemCompletedEventArgs? ev)
    {
        if (ev?.Item == null) return;
        Dispatch(ev.Item.Serial, ci => ci.OnUsingItemCompleted(ev), nameof(OnUsingItemCompleted));
    }

    private static void OnAnyUsedItem(PlayerEvents.UsedItemEventArgs? ev)
    {
        if (ev?.Item == null) return;
        Dispatch(ev.Item.Serial, ci => ci.OnUsed(ev), nameof(OnUsed));
    }

    private static void OnAnyShooting(PlayerEvents.ShootingEventArgs? ev)
    {
        var item = ev?.Player?.CurrentItem;
        if (item == null) return;
        Dispatch(item.Serial, ci => ci.OnShooting(ev!), nameof(OnShooting));
    }

    private static void OnAnyShot(PlayerEvents.ShotEventArgs? ev)
    {
        var item = ev?.Player?.CurrentItem;
        if (item == null) return;
        Dispatch(item.Serial, ci => ci.OnShot(ev!), nameof(OnShot));
    }

    private static void OnAnyHurting(PlayerEvents.HurtingEventArgs? ev)
    {
        var item = ev?.Attacker?.CurrentItem;
        if (item == null) return;
        Dispatch(item.Serial, ci => ci.OnHurtingOthers(ev!), nameof(OnHurtingOthers));
    }

    private static void OnAnyDying(PlayerEvents.DyingEventArgs? ev)
    {
        if (ev?.Player == null) return;
        foreach (var item in ev.Player.Items.ToList())
        {
            if (item == null) continue;
            if (!SerialToItem.TryGetValue(item.Serial, out var ci) || ci == null) continue;
            try { ci.OnOwnerDying(ev); }
            catch (Exception ex) { Log.Error($"CItem.OnOwnerDying error in {ci.GetType().Name}: {ex}"); }
        }
    }

    private static void OnAnyChangingItem(PlayerEvents.ChangingItemEventArgs? ev)
    {
        if (ev?.Player == null) return;

        // 旧アイテムの Hint を消去
        var oldItem = ev.Player.CurrentItem;
        if (oldItem != null
            && SerialToItem.ContainsKey(oldItem.Serial))
        {
            try { ev.Player.ShowHint(string.Empty, 0.1f); }
            catch (Exception e) { Log.Error($"CItem.ChangingItem(clearHint): {e}"); }
        }

        if (ev.Item == null) return;
        if (!SerialToItem.TryGetValue(ev.Item.Serial, out var ci) || ci == null) return;

        try { ci.OnChangingItem(ev); }
        catch (Exception e) { Log.Error($"CItem.OnChangingItem error in {ci.GetType().Name}: {e}"); }

        if (ev.IsAllowed && ci.ShowSelectedHint)
        {
            try { ci.ShowSelectedMessage(ev.Player); }
            catch (Exception e) { Log.Error($"CItem.ShowSelectedMessage error in {ci.GetType().Name}: {e}"); }
        }
    }

    private static void OnAnyThrowingRequest(PlayerEvents.ThrowingRequestEventArgs? ev)
    {
        if (ev?.Item == null) return;
        Dispatch(ev.Item.Serial, ci => ci.OnThrowingRequest(ev), nameof(OnThrowingRequest));
    }

    private static void DispatchPickupAdded(
        CItem ci,
        Pickup pickup,
        MapEvents.PickupAddedEventArgs? ev)
    {
        if (!ActivePickupAddedDispatches.Add(pickup.Serial))
            return;

        if (ev != null)
        {
            try { ci.OnPickupAdded(ev); }
            catch (Exception ex) { Log.Error($"CItem.OnPickupAdded error in {ci.GetType().Name}: {ex}"); }
        }

        if (ci.PickupLightEnabled)
            ci.AddPickupLight(pickup);

        string? schematicName = ci.ResolvePickupSchematicName(pickup);
        if (!string.IsNullOrEmpty(schematicName))
        {
            AttachPickupSchematic(
                pickup,
                schematicName!,
                ci.PickupSchematicOffset,
                ci.PickupSchematicRotationOffset,
                ci.PickupSchematicScale);
        }
    }

    private static void DispatchPickupAddedFallback(CItem ci, Pickup pickup)
    {
        MapEvents.PickupAddedEventArgs? ev = null;

        try
        {
            if (pickup.Base != null)
                ev = new MapEvents.PickupAddedEventArgs(pickup.Base);
        }
        catch (Exception ex)
        {
            Log.Warn($"[CItem] Spawn: failed to create PickupAdded fallback event for serial={pickup.Serial}: {ex.Message}");
        }

        DispatchPickupAdded(ci, pickup, ev);
    }

    /// <summary>
    /// EXILED の PickupAdded。戻り値登録済みを優先し、
    /// pending はあくまで補助として扱うようにして安全化。
    /// </summary>
    private static void OnAnyPickupAdded(MapEvents.PickupAddedEventArgs? ev)
    {
        if (ev?.Pickup == null) return;

        CItem? ci;

        // すでに Spawn/Give 経由で登録されているならそれを使う。
        if (SerialToItem.TryGetValue(ev.Pickup.Serial, out var existing) && existing != null)
        {
            ci = existing;
            Log.Debug($"[CItem] PickupAdded(tracked): serial={ev.Pickup.Serial} ci={ci.GetType().Name}");
        }
        else if (_pendingSpawnCItem != null)
        {
            // pending は補助。ここで登録しておけば最低限結びつく。
            ci = _pendingSpawnCItem;
            SerialToItem[ev.Pickup.Serial] = ci;

            Log.Debug($"[CItem] PickupAdded(spawn-pending): serial={ev.Pickup.Serial} ci={ci.GetType().Name}");
        }
        else
        {
            // 追跡対象外。静かに無視。
            return;
        }

        DispatchPickupAdded(ci, ev.Pickup, ev);
    }

    private static void OnAnyPickupDestroyed(MapEvents.PickupDestroyedEventArgs? ev)
    {
        if (ev?.Pickup == null) return;

        if (!SerialToItem.TryGetValue(ev.Pickup.Serial, out var ci) || ci == null)
        {
            Log.Debug(
                $"[CItem] PickupDestroyed(untracked): serial={ev.Pickup.Serial} " +
                $"hasLight={PickupLights.ContainsKey(ev.Pickup.Serial)}");
            return;
        }

        try { ci.OnPickupDestroyed(ev); }
        catch (Exception ex) { Log.Error($"CItem.OnPickupDestroyed error in {ci.GetType().Name}: {ex}"); }

        // PickupDestroyed は「プレイヤーが拾った」時にも発火する。
        // その場合は直後に ItemAdded → OnAcquired が来て SerialToItem を引き続き使う。
        // SerialToItem の掃除は WaitingForPlayers に任せる。

        // ライト / Schematic は Pickup が消えたら不要なので即破棄。
        ActivePickupAddedDispatches.Remove(ev.Pickup.Serial);
        bool hadLight = PickupLights.ContainsKey(ev.Pickup.Serial);
        DestroyPickupLightInternal(ev.Pickup.Serial);
        DestroyPickupSchematicInternal(ev.Pickup.Serial);

        Log.Debug(
            $"[CItem] PickupDestroyed(tracked): serial={ev.Pickup.Serial} ci={ci.GetType().Name} " +
            $"hadLight={hadLight} lightRemoved={!PickupLights.ContainsKey(ev.Pickup.Serial)}");
    }

    private static void OnAnyUpgradingPickup(Scp914Events.UpgradingPickupEventArgs? ev)
    {
        if (ev?.Pickup == null) return;
        Dispatch(ev.Pickup.Serial, ci => ci.OnUpgradingPickup(ev), nameof(OnUpgradingPickup));
    }

    private static void OnAnyUpgradingInventoryItem(Scp914Events.UpgradingInventoryItemEventArgs? ev)
    {
        if (ev?.Item == null) return;
        Dispatch(ev.Item.Serial, ci => ci.OnUpgradingInventoryItem(ev), nameof(OnUpgradingInventoryItem));
    }

    private static void OnAnyWaitingForPlayers()
    {
        // ラウンド間: 全シリアルをクリア
        SerialToItem.Clear();
        ActivePickupAddedDispatches.Clear();

        foreach (var serial in PickupLights.Keys.ToList())
            DestroyPickupLightInternal(serial);
        PickupLights.Clear();
        PickupLightCoroutines.Clear();

        foreach (var serial in PickupSchematics.Keys.ToList())
            DestroyPickupSchematicInternal(serial);
        PickupSchematics.Clear();
        PickupSchematicCoroutines.Clear();

        foreach (var ci in RegisteredInstances.ToList())
        {
            try
            {
                ci.OnWaitingForPlayers();
            }
            catch (Exception ex) { Log.Error($"CItem.OnWaitingForPlayers error in {ci.GetType().Name}: {ex}"); }
        }
    }

    // ======================================================
    // Goggles (Scp1344) ディスパッチ
    // ======================================================

    private static void OnAnyScp1344ChangedStatus(Scp1344Events.ChangedStatusEventArgs? ev)
    {
        if (ev?.Item == null) return;
        if (!SerialToItem.TryGetValue(ev.Item.Serial, out var ci) || ci == null || !ci.IsGoggles) return;

        try
        {
            switch (ev.Scp1344Status)
            {
                case Scp1344Status.Activating:
                    ev.Scp1344.Base._useTime = 5f - ci.WearingTime;
                    break;

                case Scp1344Status.Active:
                    if (ci.Remove1344Effect)
                    {
                        ev.Player.DisableEffect(EffectType.Scp1344);
                        ev.Player.ReferenceHub.EnableWearables(WearableElements.Scp1344Goggles);
                    }
                    ci.OnGogglesWorn(ev.Player, ev.Scp1344);
                    break;

                case Scp1344Status.Deactivating:
                    ev.Scp1344.Base._useTime = 5.1f - ci.RemovingTime;
                    break;
            }
        }
        catch (Exception e)
        {
            Log.Error($"CItem.OnAnyScp1344ChangedStatus error in {ci.GetType().Name}: {e}");
        }
    }

    private static void OnAnyScp1344ChangingStatus(Scp1344Events.ChangingStatusEventArgs? ev)
    {
        if (ev == null || !ev.IsAllowed || ev.Item == null) return;
        if (!SerialToItem.TryGetValue(ev.Item.Serial, out var ci) || ci == null || !ci.IsGoggles) return;

        if (ev.Scp1344StatusOld == Scp1344Status.Deactivating
            && ev.Scp1344StatusNew == Scp1344Status.Idle)
        {
            try
            {
                if (!ci.Remove1344Effect)
                    ev.Player.DisableEffect(EffectType.Scp1344);

                if (ci.CanBeRemoveSafely)
                {
                    ev.Player.DisableEffect(EffectType.Blinded);
                    if (ev.Player.ReferenceHub != null)
                        WearableSync.DisableWearables(ev.Player.ReferenceHub, WearableElements.Scp1344Goggles);
                }

                ci.OnGogglesRemoved(ev.Player, ev.Scp1344);
            }
            catch (Exception e)
            {
                Log.Error($"CItem.OnAnyScp1344ChangingStatus(remove) error in {ci.GetType().Name}: {e}");
            }
        }
    }

    private static void OnAnyScp1344Deactivating(Scp1344Events.DeactivatingEventArgs? ev)
    {
        if (ev == null || !ev.IsAllowed || ev.Item == null) return;
        if (!SerialToItem.TryGetValue(ev.Item.Serial, out var ci) || ci == null || !ci.IsGoggles) return;
        if (!ci.CanBeRemoveSafely) return;

        ev.NewStatus = Scp1344Status.Idle;
        ev.IsAllowed = false;
    }

    // ======================================================
    // SerialTracker (CItemHybrid 向け公開ヘルパー)
    // ======================================================

    public static class SerialTracker
    {
        public static void ForceRegister(ushort serial, CItem? item)
        {
            if (item == null) return;
            SerialToItem[serial] = item;
        }

        public static void ForceUnregister(ushort serial)
            => SerialToItem.Remove(serial);

        public static bool TryGet(ushort serial, out CItem? item)
            => SerialToItem.TryGetValue(serial, out item!);
    }

    // ======================================================
    // internal shim (CItemHybrid が protected メソッドを呼ぶために使う)
    // ======================================================

    internal ItemType GetBaseItem() => BaseItem;

    internal void CallOnAcquired(PlayerEvents.ItemAddedEventArgs ev, bool displayMessage) => OnAcquired(ev, displayMessage);
    internal void CallOnReleased(PlayerEvents.ItemRemovedEventArgs ev)                    => OnReleased(ev);
    internal void CallOnSpawned (Pickup pickup)                                            => OnSpawned(pickup);
    internal void CallOnPickingUp(PlayerEvents.PickingUpItemEventArgs ev)                 => OnPickingUp(ev);
    internal void CallOnDropping (PlayerEvents.DroppingItemEventArgs ev)                  => OnDropping(ev);
    internal void CallOnUsing    (PlayerEvents.UsingItemEventArgs ev)                     => OnUsing(ev);
    internal void CallOnUsingItemCompleted(PlayerEvents.UsingItemCompletedEventArgs ev)   => OnUsingItemCompleted(ev);
    internal void CallOnUsed     (PlayerEvents.UsedItemEventArgs ev)                      => OnUsed(ev);
    internal void CallOnShooting (PlayerEvents.ShootingEventArgs ev)                      => OnShooting(ev);
    internal void CallOnShot     (PlayerEvents.ShotEventArgs ev)                          => OnShot(ev);
    internal void CallOnHurtingOthers(PlayerEvents.HurtingEventArgs ev)                  => OnHurtingOthers(ev);
    internal void CallOnOwnerDying(PlayerEvents.DyingEventArgs ev)                        => OnOwnerDying(ev);
    internal void CallOnChangingItem(PlayerEvents.ChangingItemEventArgs ev)               => OnChangingItem(ev);
    internal void CallOnThrowingRequest(PlayerEvents.ThrowingRequestEventArgs ev)         => OnThrowingRequest(ev);
    internal void CallOnPickupAdded    (MapEvents.PickupAddedEventArgs ev)                => OnPickupAdded(ev);
    internal void CallOnPickupDestroyed(MapEvents.PickupDestroyedEventArgs ev)            => OnPickupDestroyed(ev);
    internal void CallOnWaitingForPlayers()                                               => OnWaitingForPlayers();
    internal void CallOnUpgradingPickup(Scp914Events.UpgradingPickupEventArgs ev)         => OnUpgradingPickup(ev);
    internal void CallOnUpgradingInventoryItem(Scp914Events.UpgradingInventoryItemEventArgs ev) => OnUpgradingInventoryItem(ev);
    internal void CallCustomizeItem(Item item)                                            => CustomizeItem(item);
    internal void CallCustomizePickup(Pickup pickup)                                      => CustomizePickup(pickup);
    internal object? CallOnCaptureState(Item item)                                         => OnCaptureState(item);
    internal void CallOnRestoreState(Item item, object? state)                            => OnRestoreState(item, state);
    internal void CallOnModeActivated(Item item)                                          => OnModeActivated(item);

    // Give() ペンディング状態を CItemHybrid から制御するための内部 API
    internal static void SetPendingGive(CItem ci, bool displayMessage)
    {
        _pendingGiveCItem          = ci;
        _pendingGiveDisplayMessage = displayMessage;
    }

    internal static void ClearPendingGive()
    {
        _pendingGiveCItem          = null;
        _pendingGiveDisplayMessage = false;
    }
}

