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
    private static readonly Dictionary<ushort, Exiled.API.Features.Toys.Light> PickupLights = new();
    private static readonly Dictionary<ushort, CoroutineHandle> PickupLightCoroutines = new();

    // Pickup に追従する Schematic
    private static readonly Dictionary<ushort, SchematicObject> PickupSchematics = new();
    private static readonly Dictionary<ushort, CoroutineHandle> PickupSchematicCoroutines = new();
    private static readonly Dictionary<SchematicPickup, CItem> SchematicPickupToItem = new();

    private static bool _eventsSubscribed;

    private readonly List<SchematicPickup> _schematicPickups = [];

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
        SchematicPickupToItem.Clear();

        if (_eventsSubscribed)
        {
            PlayerHandlers.PickingUpItem    -= OnAnyPickingUpItem;
            PlayerHandlers.ItemAdded        -= OnAnyItemAdded;
            PlayerHandlers.ItemRemoved      -= OnAnyItemRemoved;
            PlayerHandlers.DroppingItem     -= OnAnyDroppingItem;
            PlayerHandlers.UsingItem        -= OnAnyUsingItem;
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
    public static void OverrideItemInstance(string uniqueKey, CItem instance)
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
    protected virtual float        PickupLightRange      => 5f;
    protected virtual LightShadows PickupLightShadowType => LightShadows.None;

    // ======================================================
    // Pickup Schematic 設定
    // ======================================================

    protected virtual string? PickupSchematicName => null;
    protected virtual Vector3 PickupSchematicOffset => Vector3.zero;
    protected virtual Vector3 PickupSchematicRotationOffset => Vector3.zero;
    protected virtual Vector3 PickupSchematicScale => Vector3.one;

    protected virtual bool UseSchematicPickup => false;
    protected virtual Vector3 SchematicPickupInteractableOffset => Vector3.zero;
    protected virtual Vector3 SchematicPickupInteractableRotationOffset => Vector3.zero;
    protected virtual Vector3 SchematicPickupInteractableScale => Vector3.one;
    protected virtual AdminToys.InvisibleInteractableToy.ColliderShape SchematicPickupInteractableShape =>
        AdminToys.InvisibleInteractableToy.ColliderShape.Box;
    protected virtual float SchematicPickupInteractionDuration => 0.25f;
    protected virtual bool SchematicPickupRequireInventorySpace => true;
    protected virtual bool SchematicPickupDestroyAfterSuccessfulSearch => true;
    protected virtual string SchematicPickupInventoryFullHint => "<size=24>インベントリがいっぱいです。</size>";
    protected virtual float SchematicPickupInventoryFullHintDuration => 2f;
    protected virtual bool SchematicPickupUseBackingPickup => true;
    protected virtual bool SchematicPickupHideBackingPickup => true;
    protected virtual Vector3 SchematicPickupHiddenBackingPickupScale => Vector3.zero;
    protected virtual Vector3 SchematicPickupBackingPickupScale => Vector3.one;

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

        DestroySchematicPickups();
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
    /// AddItem の呼び出しで同期的に ItemAdded が発火し、
    /// OnAnyItemAdded 内で SerialToItem 登録と OnAcquired ディスパッチが完了する。
    /// </summary>
    public virtual Item? Give(Player? player, bool displayMessage = false)
    {
        if (player == null) return null;

        // ペンディング状態をセット。finally で必ずクリアする。
        _pendingGiveCItem          = this;
        _pendingGiveDisplayMessage = displayMessage;
        try
        {
            var item = player.AddItem(BaseItem);
            if (item == null) return null;

            // AddItem は同期で ItemAdded を発火するため、
            // ここに到達した時点では SerialToItem は既に登録済みのはず。
            // 万一 ItemAdded を拾えなかった場合の保険。
            if (!SerialToItem.ContainsKey(item.Serial))
            {
                SerialToItem[item.Serial] = this;
                Log.Warn($"[CItem] Give: ItemAdded missed for serial={item.Serial}, force-registered.");
            }

            CustomizeItem(item);
            return item;
        }
        catch (Exception ex)
        {
            Log.Error($"CItem.Give failed ({GetType().Name}): {ex}");
            return null;
        }
        finally
        {
            // 同期発火の ItemAdded がすでにクリアしている場合でも二重クリアは無害。
            _pendingGiveCItem          = null;
            _pendingGiveDisplayMessage = false;
        }
    }

    /// <summary>指定位置にこの CItem の Pickup を生成する。</summary>
    public virtual Pickup? Spawn(Vector3 position)
    {
        if (UseSchematicPickup)
        {
            SpawnSchematicPickup(position);
            return null;
        }

        _pendingSpawnCItem = this;
        try
        {
            var pickup = CreatePickupForSpawn(position);
            if (pickup == null) return null;

            // CreatePickupForSpawn は内部で PickupAdded を同期発火するため、
            // OnAnyPickupAdded 内で SerialToItem 登録済みのはず。保険登録。
            if (!SerialToItem.ContainsKey(pickup.Serial))
            {
                SerialToItem[pickup.Serial] = this;
                Log.Warn($"[CItem] Spawn: PickupAdded missed for serial={pickup.Serial}, force-registered.");
            }

            CustomizePickup(pickup);
            OnSpawned(pickup);
            return pickup;
        }
        catch (Exception ex)
        {
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

    // ======================================================
    // Pickup ライト制御
    // ======================================================

    public virtual Exiled.API.Features.Toys.Light? AddPickupLight(Pickup? pickup)
    {
        if (pickup == null) return null;
        if (PickupLights.TryGetValue(pickup.Serial, out var existing)) return existing;

        try
        {
            var light = Exiled.API.Features.Toys.Light.Create(pickup.Position);
            if (light == null) return null;

            light.Color      = PickupLightColor;
            light.Intensity  = PickupLightIntensity;
            light.Range      = PickupLightRange;
            light.ShadowType = PickupLightShadowType;

            var serial = pickup.Serial;
            PickupLights[serial]          = light;
            PickupLightCoroutines[serial] = Timing.RunCoroutine(TrackPickupLight(pickup, light));

            return light;
        }
        catch (Exception ex)
        {
            Log.Error($"CItem.AddPickupLight failed ({GetType().Name}): {ex}");
            return null;
        }
    }

    private static IEnumerator<float> TrackPickupLight(
        Pickup pickup, Exiled.API.Features.Toys.Light light)
    {
        while (pickup?.Base?.gameObject != null && light?.Base?.gameObject != null)
        {
            light.Position = pickup.Position;
            yield return Timing.WaitForOneFrame;
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
        Vector3 scale)
    {
        if (PickupSchematics.ContainsKey(pickup.Serial)) return;

        try
        {
            var rotation = pickup.Rotation * Quaternion.Euler(rotationOffset);
            var schem = ObjectSpawner.SpawnSchematic(
                schematicName,
                pickup.Position + pickup.Rotation * offset,
                rotation,
                scale);
            if (schem == null) return;

            PickupSchematics[pickup.Serial]          = schem;
            PickupSchematicCoroutines[pickup.Serial] = Timing.RunCoroutine(
                TrackPickupSchematic(pickup, schem, offset, rotationOffset));
        }
        catch (Exception ex)
        {
            Log.Error($"CItem.AttachPickupSchematic({schematicName}) failed: {ex}");
        }
    }

    public virtual SchematicPickup? SpawnSchematicPickup(Vector3 position)
    {
        if (string.IsNullOrEmpty(PickupSchematicName))
        {
            Log.Warn($"CItem.SpawnSchematicPickup: {GetType().Name} has empty PickupSchematicName.");
            return null;
        }

        _pendingSpawnCItem = this;
        SchematicPickup? pickup;
        try
        {
            pickup = SchematicPickup.Spawn(CreateSchematicPickupSettings(position));
            if (pickup == null) return null;
        }
        finally
        {
            _pendingSpawnCItem = null;
        }

        if (pickup.BackingPickup != null)
        {
            if (!SerialToItem.ContainsKey(pickup.BackingPickup.Serial))
                SerialToItem[pickup.BackingPickup.Serial] = this;

            CustomizePickup(pickup.BackingPickup);
            OnSpawned(pickup.BackingPickup);
        }

        SchematicPickupToItem[pickup] = this;
        _schematicPickups.Add(pickup);
        pickup.Searched += OnInternalSchematicPickupSearched;
        pickup.Destroyed += OnInternalSchematicPickupDestroyed;
        OnSchematicPickupSpawned(pickup);
        return pickup;
    }

    protected virtual SchematicPickupSettings CreateSchematicPickupSettings(Vector3 position)
        => new()
        {
            SchematicName = PickupSchematicName ?? string.Empty,
            Position = position,
            SchematicOffset = PickupSchematicOffset,
            SchematicRotationOffset = PickupSchematicRotationOffset,
            SchematicScale = PickupSchematicScale,
            InteractableOffset = SchematicPickupInteractableOffset,
            InteractableRotationOffset = SchematicPickupInteractableRotationOffset,
            InteractableScale = SchematicPickupInteractableScale,
            InteractableShape = SchematicPickupInteractableShape,
            InteractionDuration = SchematicPickupInteractionDuration,
            RequireInventorySpace = SchematicPickupRequireInventorySpace,
            DestroyAfterSuccessfulSearch = SchematicPickupDestroyAfterSuccessfulSearch,
            InventoryFullHint = SchematicPickupInventoryFullHint,
            InventoryFullHintDuration = SchematicPickupInventoryFullHintDuration,
            BackingPickupItemType = SchematicPickupUseBackingPickup ? BaseItem : null,
            HideBackingPickup = SchematicPickupHideBackingPickup,
            HiddenBackingPickupScale = SchematicPickupHiddenBackingPickupScale,
            BackingPickupScale = SchematicPickupBackingPickupScale,
            OnSearched = TryAcquireSchematicPickup,
        };

    protected virtual bool TryAcquireSchematicPickup(SchematicPickup pickup, Player player)
    {
        if (!OnSchematicPickupPickingUp(pickup, player))
            return false;

        var item = Give(player, displayMessage: true);
        if (item == null) return false;

        OnSchematicPickupAcquired(pickup, player, item);
        return true;
    }

    protected virtual void OnSchematicPickupSpawned(SchematicPickup pickup) { }

    protected virtual bool OnSchematicPickupPickingUp(SchematicPickup pickup, Player player) => true;

    protected virtual void OnSchematicPickupSearched(SchematicPickup pickup, Player player) { }

    protected virtual void OnSchematicPickupAcquired(SchematicPickup pickup, Player player, Item item) { }

    protected virtual void OnSchematicPickupDestroyed(SchematicPickup pickup) { }

    private void OnInternalSchematicPickupSearched(SchematicPickup pickup, Player player)
    {
        try { OnSchematicPickupSearched(pickup, player); }
        catch (Exception ex) { Log.Error($"CItem.OnSchematicPickupSearched error in {GetType().Name}: {ex}"); }
    }

    private void OnInternalSchematicPickupDestroyed(SchematicPickup pickup)
    {
        pickup.Searched -= OnInternalSchematicPickupSearched;
        pickup.Destroyed -= OnInternalSchematicPickupDestroyed;
        _schematicPickups.Remove(pickup);
        SchematicPickupToItem.Remove(pickup);

        try { OnSchematicPickupDestroyed(pickup); }
        catch (Exception ex) { Log.Error($"CItem.OnSchematicPickupDestroyed error in {GetType().Name}: {ex}"); }
    }

    private void DestroySchematicPickups()
    {
        foreach (var pickup in _schematicPickups.ToArray())
            pickup.Destroy();

        _schematicPickups.Clear();
    }

    private static IEnumerator<float> TrackPickupSchematic(
        Pickup pickup,
        SchematicObject schem,
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

            SerialToItem.Remove(item.Serial);
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

    public bool Check(SchematicPickup? pickup)
    {
        if (pickup == null) return false;
        if (!SchematicPickupToItem.TryGetValue(pickup, out var ci) || ci == null) return false;
        if (ReferenceEquals(ci, this)) return true;
        return ci is CItemHybrid hybrid
               && pickup.BackingPickup != null
               && hybrid.IsCurrentSub(pickup.BackingPickup.Serial, this);
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

    public static bool TryGet(SchematicPickup? pickup, out CItem? cItem)
    {
        cItem = null;
        return pickup != null && SchematicPickupToItem.TryGetValue(pickup, out cItem!);
    }

    public static bool TryGetByKey(string uniqueKey, out CItem? cItem)
        => UniqueKeyToItem.TryGetValue(uniqueKey, out cItem!);

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

    private static void OnAnyPickingUpItem(PlayerEvents.PickingUpItemEventArgs ev)
    {
        if (ev?.Pickup == null) return;
        Dispatch(ev.Pickup.Serial, ci => ci.OnPickingUp(ev), nameof(OnPickingUp));
    }

    /// <summary>
    /// EXILED のイベント発火順（拾い直し時）:
    ///   1. PickingUpItem
    ///   2. ItemAdded      ← ここで SerialToItem 登録 + OnAcquired
    ///   3. PickupDestroyed← この時点ではすでに「拾い直し済み」
    ///
    /// そのため PickupDestroyed 到達時に SerialToItem から消してはいけない。
    /// SerialToItem のクリアは WaitingForPlayers で一括して行う。
    /// </summary>
    private static void OnAnyItemAdded(PlayerEvents.ItemAddedEventArgs ev)
    {
        if (ev?.Player == null || ev.Item == null) return;

        CItem? ci;
        bool   displayMessage;

        if (_pendingGiveCItem != null)
        {
            // Give() 由来: ペンディングマーカーから CItem を確定
            ci            = _pendingGiveCItem;
            displayMessage = _pendingGiveDisplayMessage;

            // ペンディングをクリア（Give() の finally でも行うが先にクリアして安全に）
            _pendingGiveCItem          = null;
            _pendingGiveDisplayMessage = false;

            SerialToItem[ev.Item.Serial] = ci;
            Log.Debug($"[CItem] ItemAdded(give): serial={ev.Item.Serial} ci={ci.GetType().Name}");
        }
        else if (SerialToItem.TryGetValue(ev.Item.Serial, out var existing) && existing != null)
        {
            // 床からの拾い直し / SCP-914 経由 / 他プラグイン由来 で
            // 既に SerialToItem にある場合（Pickup として追跡中だったケース）
            ci            = existing;
            displayMessage = true;
            Log.Debug($"[CItem] ItemAdded(tracked): serial={ev.Item.Serial} ci={ci.GetType().Name}");
        }
        else
        {
            // 追跡対象外のアイテム
            Log.Debug($"[CItem] ItemAdded(unknown): serial={ev.Item.Serial}");
            return;
        }

        try { ci.OnAcquired(ev, displayMessage); }
        catch (Exception e) { Log.Error($"CItem.OnAcquired error in {ci.GetType().Name}: {e}"); }

        if (displayMessage && ci.ShowPickedUpHint)
        {
            try { ci.ShowPickedUpMessage(ev.Player); }
            catch (Exception e) { Log.Error($"CItem.ShowPickedUpMessage error in {ci.GetType().Name}: {e}"); }
        }
    }

    private static void OnAnyItemRemoved(PlayerEvents.ItemRemovedEventArgs ev)
    {
        if (ev?.Item == null) return;
        // ItemRemoved は「インベントリから抜けた」通知。
        // 対応 Pickup への遷移中の可能性があるため SerialToItem は削除しない。
        // 本当の消滅は WaitingForPlayers で一括クリアする。
        if (!SerialToItem.TryGetValue(ev.Item.Serial, out var ci) || ci == null) return;
        try { ci.OnReleased(ev); }
        catch (Exception e) { Log.Error($"CItem.OnReleased error in {ci.GetType().Name}: {e}"); }
    }

    private static void OnAnyDroppingItem(PlayerEvents.DroppingItemEventArgs ev)
    {
        if (ev?.Item == null) return;
        Dispatch(ev.Item.Serial, ci => ci.OnDropping(ev), nameof(OnDropping));
    }

    private static void OnAnyUsingItem(PlayerEvents.UsingItemEventArgs ev)
    {
        if (ev?.Item == null) return;
        Dispatch(ev.Item.Serial, ci => ci.OnUsing(ev), nameof(OnUsing));
    }

    private static void OnAnyUsedItem(PlayerEvents.UsedItemEventArgs ev)
    {
        if (ev?.Item == null) return;
        Dispatch(ev.Item.Serial, ci => ci.OnUsed(ev), nameof(OnUsed));
    }

    private static void OnAnyShooting(PlayerEvents.ShootingEventArgs ev)
    {
        var item = ev?.Player?.CurrentItem;
        if (item == null) return;
        Dispatch(item.Serial, ci => ci.OnShooting(ev!), nameof(OnShooting));
    }

    private static void OnAnyShot(PlayerEvents.ShotEventArgs ev)
    {
        var item = ev?.Player?.CurrentItem;
        if (item == null) return;
        Dispatch(item.Serial, ci => ci.OnShot(ev!), nameof(OnShot));
    }

    private static void OnAnyHurting(PlayerEvents.HurtingEventArgs ev)
    {
        var item = ev?.Attacker?.CurrentItem;
        if (item == null) return;
        Dispatch(item.Serial, ci => ci.OnHurtingOthers(ev!), nameof(OnHurtingOthers));
    }

    private static void OnAnyDying(PlayerEvents.DyingEventArgs ev)
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

    private static void OnAnyChangingItem(PlayerEvents.ChangingItemEventArgs ev)
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

    private static void OnAnyThrowingRequest(PlayerEvents.ThrowingRequestEventArgs ev)
    {
        if (ev?.Item == null) return;
        Dispatch(ev.Item.Serial, ci => ci.OnThrowingRequest(ev), nameof(OnThrowingRequest));
    }

    private static void OnAnyPickupAdded(MapEvents.PickupAddedEventArgs ev)
    {
        if (ev?.Pickup == null) return;

        CItem? ci;

        if (_pendingSpawnCItem != null)
        {
            // Spawn() 由来
            ci = _pendingSpawnCItem;
            SerialToItem[ev.Pickup.Serial] = ci;
            Log.Debug($"[CItem] PickupAdded(spawn): serial={ev.Pickup.Serial} ci={ci.GetType().Name}");
        }
        else if (SerialToItem.TryGetValue(ev.Pickup.Serial, out var existing) && existing != null)
        {
            // ドロップ由来（インベントリ→床への遷移）
            ci = existing;
            Log.Debug($"[CItem] PickupAdded(drop): serial={ev.Pickup.Serial} ci={ci.GetType().Name}");
        }
        else
        {
            return; // 追跡対象外
        }

        try { ci.OnPickupAdded(ev); }
        catch (Exception ex) { Log.Error($"CItem.OnPickupAdded error in {ci.GetType().Name}: {ex}"); }

        if (ci.PickupLightEnabled)
            ci.AddPickupLight(ev.Pickup);

        if (!ci.UseSchematicPickup && !string.IsNullOrEmpty(ci.PickupSchematicName))
        {
            AttachPickupSchematic(
                ev.Pickup,
                ci.PickupSchematicName!,
                ci.PickupSchematicOffset,
                ci.PickupSchematicRotationOffset,
                ci.PickupSchematicScale);
        }
    }

    private static void OnAnyPickupDestroyed(MapEvents.PickupDestroyedEventArgs ev)
    {
        if (ev?.Pickup == null) return;
        if (!SerialToItem.TryGetValue(ev.Pickup.Serial, out var ci) || ci == null) return;

        try { ci.OnPickupDestroyed(ev); }
        catch (Exception ex) { Log.Error($"CItem.OnPickupDestroyed error in {ci.GetType().Name}: {ex}"); }

        // PickupDestroyed は「プレイヤーが拾った」時にも発火する。
        // その場合は直後に ItemAdded → OnAcquired が来て SerialToItem を引き続き使う。
        // SerialToItem の掃除は WaitingForPlayers に任せる。

        // ライト / Schematic は Pickup が消えたら不要なので即破棄。
        DestroyPickupLightInternal(ev.Pickup.Serial);
        DestroyPickupSchematicInternal(ev.Pickup.Serial);
    }

    private static void OnAnyUpgradingPickup(Scp914Events.UpgradingPickupEventArgs ev)
    {
        if (ev?.Pickup == null) return;
        Dispatch(ev.Pickup.Serial, ci => ci.OnUpgradingPickup(ev), nameof(OnUpgradingPickup));
    }

    private static void OnAnyUpgradingInventoryItem(Scp914Events.UpgradingInventoryItemEventArgs ev)
    {
        if (ev?.Item == null) return;
        Dispatch(ev.Item.Serial, ci => ci.OnUpgradingInventoryItem(ev), nameof(OnUpgradingInventoryItem));
    }

    private static void OnAnyWaitingForPlayers()
    {
        // ラウンド間: 全シリアルをクリア
        SerialToItem.Clear();

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
                ci.DestroySchematicPickups();
                ci.OnWaitingForPlayers();
            }
            catch (Exception ex) { Log.Error($"CItem.OnWaitingForPlayers error in {ci.GetType().Name}: {ex}"); }
        }

        SchematicPickupToItem.Clear();
    }

    // ======================================================
    // Goggles (Scp1344) ディスパッチ
    // ======================================================

    private static void OnAnyScp1344ChangedStatus(Scp1344Events.ChangedStatusEventArgs ev)
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

    private static void OnAnyScp1344ChangingStatus(Scp1344Events.ChangingStatusEventArgs ev)
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

    private static void OnAnyScp1344Deactivating(Scp1344Events.DeactivatingEventArgs ev)
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
        public static void ForceRegister(ushort serial, CItem item)
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
