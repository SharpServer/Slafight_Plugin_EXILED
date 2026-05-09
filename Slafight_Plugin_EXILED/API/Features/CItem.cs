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
using Slafight_Plugin_EXILED.Changes;
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
    // 登録済み全インスタンス
    private static readonly HashSet<CItem> RegisteredInstances = [];

    // 全 CItem 派生タイプ
    private static readonly List<Type> ItemTypes;

    // UniqueKey → インスタンス
    private static readonly Dictionary<string, CItem> UniqueKeyToItem =
        new(StringComparer.OrdinalIgnoreCase);

    // 追跡中のシリアル → インスタンス
    private static readonly Dictionary<ushort, CItem> SerialToItem = new();

    // Pickup の Light オブジェクト管理: Serial → Light
    private static readonly Dictionary<ushort, Exiled.API.Features.Toys.Light> PickupLights = new();

    // Pickup ライトの追従コルーチンハンドル
    private static readonly Dictionary<ushort, CoroutineHandle> PickupLightCoroutines = new();

    // Pickup に追従中の Schematic: Serial → SchematicObject / コルーチン
    private static readonly Dictionary<ushort, SchematicObject> PickupSchematics = new();
    private static readonly Dictionary<ushort, CoroutineHandle> PickupSchematicCoroutines = new();

    private static bool _eventsSubscribed;

    static CItem()
    {
        var asm = typeof(CItem).Assembly;
        ItemTypes = asm.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(CItem)) && !t.IsAbstract)
            .ToList();
    }

    /// <summary>
    /// 自動登録を除外したい CItem 用属性。
    /// 手動で new して OverrideItemInstance で差し替える場合に付ける。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CItemAutoRegisterIgnoreAttribute : Attribute { }

    // ==== Plugin から呼ぶ入り口 ====

    public static void RegisterAllItems()
    {
        if (!_eventsSubscribed)
        {
            PlayerHandlers.PickingUpItem += OnAnyPickingUpItem;
            PlayerHandlers.ItemAdded += OnAnyItemAdded;
            PlayerHandlers.ItemRemoved += OnAnyItemRemoved;
            PlayerHandlers.DroppingItem += OnAnyDroppingItem;
            PlayerHandlers.UsingItem += OnAnyUsingItem;
            PlayerHandlers.UsedItem += OnAnyUsedItem;
            PlayerHandlers.Shooting += OnAnyShooting;
            PlayerHandlers.Shot += OnAnyShot;
            PlayerHandlers.Hurting += OnAnyHurting;
            PlayerHandlers.Dying += OnAnyDying;
            PlayerHandlers.ChangingItem += OnAnyChangingItem;
            PlayerHandlers.ThrowingRequest += OnAnyThrowingRequest;

            MapHandlers.PickupAdded += OnAnyPickupAdded;
            MapHandlers.PickupDestroyed += OnAnyPickupDestroyed;

            // SCP-914 アップグレードを CItem 側でも直接ハンドル。
            // Exiled の CustomKeycard は SCP-914 で正常に処理されない既知バグ
            // (ExMod-Team/EXILED#718) があるため、CItem は独自のシリアル追跡で
            // 同じ挙動を自前実装して回避する。
            Scp914Handlers.UpgradingPickup += OnAnyUpgradingPickup;
            Scp914Handlers.UpgradingInventoryItem += OnAnyUpgradingInventoryItem;

            // Goggles (Scp1344) 装着・取り外しのライフサイクル。
            // IsGoggles=true な CItem 派生だけが OnGogglesWorn / OnGogglesRemoved を受ける。
            Scp1344Handlers.ChangedStatus += OnAnyScp1344ChangedStatus;
            Scp1344Handlers.ChangingStatus += OnAnyScp1344ChangingStatus;
            Scp1344Handlers.Deactivating += OnAnyScp1344Deactivating;

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

        if (_eventsSubscribed)
        {
            PlayerHandlers.PickingUpItem -= OnAnyPickingUpItem;
            PlayerHandlers.ItemAdded -= OnAnyItemAdded;
            PlayerHandlers.ItemRemoved -= OnAnyItemRemoved;
            PlayerHandlers.DroppingItem -= OnAnyDroppingItem;
            PlayerHandlers.UsingItem -= OnAnyUsingItem;
            PlayerHandlers.UsedItem -= OnAnyUsedItem;
            PlayerHandlers.Shooting -= OnAnyShooting;
            PlayerHandlers.Shot -= OnAnyShot;
            PlayerHandlers.Hurting -= OnAnyHurting;
            PlayerHandlers.Dying -= OnAnyDying;
            PlayerHandlers.ChangingItem -= OnAnyChangingItem;
            PlayerHandlers.ThrowingRequest -= OnAnyThrowingRequest;

            MapHandlers.PickupAdded -= OnAnyPickupAdded;
            MapHandlers.PickupDestroyed -= OnAnyPickupDestroyed;

            Scp914Handlers.UpgradingPickup -= OnAnyUpgradingPickup;
            Scp914Handlers.UpgradingInventoryItem -= OnAnyUpgradingInventoryItem;

            Scp1344Handlers.ChangedStatus -= OnAnyScp1344ChangedStatus;
            Scp1344Handlers.ChangingStatus -= OnAnyScp1344ChangingStatus;
            Scp1344Handlers.Deactivating -= OnAnyScp1344Deactivating;

            ServerHandlers.WaitingForPlayers -= OnAnyWaitingForPlayers;

            _eventsSubscribed = false;
        }
    }

    /// <summary>
    /// Ignore 属性付き CItem 用:
    /// 手動で生成したインスタンスを UniqueKey マップの「本体」として登録する。
    /// </summary>
    public static void OverrideItemInstance(string uniqueKey, CItem instance)
    {
        if (string.IsNullOrEmpty(uniqueKey) || instance == null) return;
        UniqueKeyToItem[uniqueKey] = instance;
        Log.Debug($"CItem.OverrideItemInstance: {uniqueKey} -> {instance.GetType().Name}");
    }
    
    /// <summary>登録済みすべての CItem インスタンスを readonly で返す。</summary>
    public static IReadOnlyCollection<CItem> GetAllInstances()
        => RegisteredInstances;

    // ==== 派生クラスが実装/上書きするメタ情報 ====

    /// <summary>このアイテムを一意に識別するキー。</summary>
    protected abstract string UniqueKey { get; }

    /// <summary>ベースとなる素の ItemType。</summary>
    protected abstract ItemType BaseItem { get; }

    /// <summary>表示名。ログと Hint 用。</summary>
    public virtual string DisplayName => UniqueKey;

    /// <summary>アイテムの説明。ピックアップ/選択 Broadcast に差し込まれる。</summary>
    public virtual string Description => string.Empty;

    public string UniqueKeyName => UniqueKey;

    // ==== ヒント表示 (Exiled CustomItem の PickedUpHint / SelectedHint に書式を合わせる) ====

    private const string PickedUpHintFormat = "<size=24>あなたは{0}を拾いました！\n{1}</size>";
    private const float PickedUpHintDuration = 6f;

    private const string SelectedHintFormat = "<size=24>あなたは{0}を選択しました！\n{1}</size>";
    private const float SelectedHintDuration = 5f;

    /// <summary>拾ったときに Hint を自動表示するか。</summary>
    protected virtual bool ShowPickedUpHint => true;

    /// <summary>選択（手に持つ）したときに Hint を自動表示するか。</summary>
    protected virtual bool ShowSelectedHint => true;

    // ==== Pickup ライト制御 ====

    /// <summary>Pickup にライトを表示するか。</summary>
    protected virtual bool PickupLightEnabled => false;

    /// <summary>Pickup ライトの色。</summary>
    protected virtual Color PickupLightColor => Color.white;

    /// <summary>Pickup ライトの明度（輝度）。</summary>
    protected virtual float PickupLightIntensity => 0.7f;

    /// <summary>Pickup ライトの光の範囲。</summary>
    protected virtual float PickupLightRange => 5f;

    /// <summary>Pickup ライトの影の種類。</summary>
    protected virtual LightShadows PickupLightShadowType => LightShadows.None;

    // ==== Schematic 追従 (Pickup に SchematicObject を貼り付ける) ====

    /// <summary>
    /// Pickup に追従させる ProjectMER schematic 名。null/空なら attach しない。
    /// 設定すると Pickup 生成時に自動 spawn → 毎フレーム位置追従、Pickup 破棄時に
    /// schematic も破棄する。
    /// </summary>
    protected virtual string? PickupSchematicName => null;

    // ==== Goggles (Scp1344) ライフサイクル ====

    /// <summary>true なら本派生を Scp1344 (ゴーグル) として扱い、装着/取り外しイベントを受ける。</summary>
    protected virtual bool IsGoggles => false;

    /// <summary>装着完了までの秒数。デフォルト 5s で通常 SCP-1344 と同等。</summary>
    protected virtual float WearingTime => 5f;

    /// <summary>取り外し完了までの秒数。デフォルト 5.1s で通常 SCP-1344 と同等。</summary>
    protected virtual float RemovingTime => 5.1f;

    /// <summary>装着時に SCP-1344 視覚効果 (青視野) を打ち消すか。</summary>
    protected virtual bool Remove1344Effect => true;

    /// <summary>取り外し時に Blinded を消すか (false なら通常 SCP-1344 同様の盲目発生)。</summary>
    protected virtual bool CanBeRemoveSafely => true;

    /// <summary>派生クラスがゴーグル装着完了タイミングをフックしたい場合用。</summary>
    protected virtual void OnGogglesWorn(Player player, Scp1344 goggles) { }

    /// <summary>派生クラスがゴーグル取り外しタイミングをフックしたい場合用。</summary>
    protected virtual void OnGogglesRemoved(Player player, Scp1344 goggles) { }

    // ==== Armor カスタマイズ ====

    /// <summary>ベスト (体) アーマーの効力 0-100。負値なら override 無し。</summary>
    protected virtual int VestEfficacy => -1;

    /// <summary>ヘルメット (頭) アーマーの効力 0-100。負値なら override 無し。</summary>
    protected virtual int HelmetEfficacy => -1;

    /// <summary>スタミナ消費倍率 (1f 標準、1f より大きいほど消費が速い)。負値なら override 無し。</summary>
    protected virtual float StaminaUseMultiplier => -1f;

    /// <summary>拾った瞬間に出す Hint メッセージを生成。</summary>
    protected virtual string BuildPickedUpMessage()
        => string.Format(PickedUpHintFormat, DisplayName, Description);

    /// <summary>選択（手に持った）時に出す Hint メッセージを生成。</summary>
    protected virtual string BuildSelectedMessage()
        => string.Format(SelectedHintFormat, DisplayName, Description);

    /// <summary>拾った時の Hint を実際に表示する。派生でまるごと差し替え可能。</summary>
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

    /// <summary>選択した時の Hint を実際に表示する。派生でまるごと差し替え可能。</summary>
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

    /// <summary>
    /// 拾得 Hint の表示時間が終わった直後に呼ばれる。
    /// ShowPickedUpHint が false の場合は発火しない。
    /// </summary>
    protected virtual void OnPickedUpHintFinished(Player player) { }

    /// <summary>
    /// 選択 Hint の表示時間が終わった直後に呼ばれる。
    /// ShowSelectedHint が false の場合は発火しない。
    /// </summary>
    protected virtual void OnSelectedHintFinished(Player player) { }

    // ==== インスタンス管理 ====

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

        // このインスタンスで追跡していたシリアルを掃除
        var mine = SerialToItem.Where(kv => ReferenceEquals(kv.Value, this))
                               .Select(kv => kv.Key)
                               .ToList();
        foreach (var s in mine) SerialToItem.Remove(s);

        UnregisterEvents();
        Log.Debug($"CItem unregistered: {GetType().Name}");
    }

    /// <summary>派生クラスが追加イベント購読をしたい場合は override する。</summary>
    public virtual void RegisterEvents() { }

    /// <summary>派生クラスの追加イベント購読解除。</summary>
    public virtual void UnregisterEvents() { }

    // ==== 付与/生成 ====

    // Give() が実行中に走る AddItem → ItemAdded イベントに対し、
    // 「この ItemAdded は Give() 由来」だと伝えるための一時マーカー。
    // ゲームロジックは単一スレッドなので static で競合しない。
    private static CItem? _pendingGiveCItem;
    private static bool _pendingGiveDisplayMessage;

    // Spawn() が Pickup.CreateAndSpawn を呼ぶと、その中で PickupAdded が同期発火する。
    // その時点では SerialToItem への登録がまだなので、OnAnyPickupAdded 側で CItem を
    // 解決できるように一時マーカーを立てておく。
    private static CItem? _pendingSpawnCItem;

    /// <summary>
    /// プレイヤーにこの CItem を付与する。
    /// Exiled CustomItem と同じく、既定で PickedUp 相当の Hint を表示する。
    /// </summary>
    /// <param name="player">付与先プレイヤー。</param>
    /// <param name="displayMessage">true なら ShowPickedUpHint に従い Hint を表示する。</param>
    public virtual Item? Give(Player? player, bool displayMessage = false)
    {
        if (player == null) return null;

        try
        {
            _pendingGiveCItem = this;
            _pendingGiveDisplayMessage = displayMessage;

            var item = player.AddItem(BaseItem);
            if (item == null) return null;

            // AddItem 同期で ItemAdded が発火し OnAnyItemAdded 側で
            // SerialToItem 登録と OnAcquired ディスパッチが済むのが正常系。
            // 何らかの理由で ItemAdded を拾えていなかった場合に備えて保険でここでも登録。
            if (!SerialToItem.ContainsKey(item.Serial))
                SerialToItem[item.Serial] = this;

            // 派生用カスタマイズ (Keycard の Label/Tint 等)。
            // 注: OnAcquired は AddItem で先に発火しているため、CustomizeItem
            // 適用後の状態を OnAcquired 内で読みたい場合は OnAcquired ではなく
            // OnSelectedHintFinished など別タイミングを使うこと。
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
            _pendingGiveCItem = null;
            _pendingGiveDisplayMessage = false;
        }
    }

    /// <summary>指定位置にこの CItem の Pickup を生成する。</summary>
    public virtual Pickup? Spawn(Vector3 position)
    {
        try
        {
            // Pickup.CreateAndSpawn (および派生 override) は内部で PickupAdded を
            // 同期発火するため、そのタイミングで OnAnyPickupAdded 側で CItem を
            // 解決できるよう事前にマーカーを立てておく。
            _pendingSpawnCItem = this;

            var pickup = CreatePickupForSpawn(position);
            if (pickup == null) return null;

            // OnAnyPickupAdded が先に SerialToItem を登録しているはずだが、
            // 万一発火していなかった場合の保険としてここでも登録する。
            if (!SerialToItem.ContainsKey(pickup.Serial))
                SerialToItem[pickup.Serial] = this;

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
    /// 派生クラスが Pickup の生成方法を差し替えたい場合のフック。
    /// デフォルトは <see cref="Pickup.CreateAndSpawn(ItemType, Vector3, Quaternion)"/>。
    /// CItemKeycard など <see cref="Item.Create(ItemType, Player)"/> + 個別カスタマイズ +
    /// <see cref="Item.CreatePickup(Vector3, Quaternion?, bool)"/> 経由で生成したい
    /// 派生はこのメソッドを override する。
    /// </summary>
    protected virtual Pickup? CreatePickupForSpawn(Vector3 position)
        => Pickup.CreateAndSpawn(BaseItem, position, Quaternion.identity);

    /// <summary>
    /// プレイヤーへ <see cref="Give"/> でアイテムが渡った直後 / <see cref="Spawn"/>
    /// で生成される Pickup の元となる Item に対して呼ばれる派生フック。
    /// Keycard の Label / Tint など Item ベースで設定する必要のあるカスタマイズを
    /// ここで適用する。デフォルト実装は Armor 系の VestEfficacy / HelmetEfficacy /
    /// StaminaUseMultiplier を該当アイテムに焼き付けるので、override する派生は
    /// 必ず <c>base.CustomizeItem(item)</c> を呼ぶこと。
    /// </summary>
    protected virtual void CustomizeItem(Item item)
    {
        if (item is Armor armor)
        {
            if (VestEfficacy >= 0)
                armor.VestEfficacy = VestEfficacy;
            if (HelmetEfficacy >= 0)
                armor.HelmetEfficacy = HelmetEfficacy;
            if (StaminaUseMultiplier >= 0f)
                armor.StaminaUseMultiplier = StaminaUseMultiplier;
        }
    }

    /// <summary>
    /// 指定 Pickup にライトを追加する。既にライトがあれば既存のものを返す。
    /// Pickup.CreateAndSpawn 経由で作られた Pickup は SetParent しても
    /// クライアントに親子関係が同期されないため、毎フレーム位置追従する
    /// コルーチンで Pickup に Light を追従させる。
    /// </summary>
    public virtual Exiled.API.Features.Toys.Light? AddPickupLight(Pickup? pickup)
    {
        if (pickup == null) return null;

        if (PickupLights.TryGetValue(pickup.Serial, out var existing))
            return existing;

        try
        {
            var light = Exiled.API.Features.Toys.Light.Create(pickup.Position);
            if (light == null) return null;

            light.Color = PickupLightColor;
            light.Intensity = PickupLightIntensity;
            light.Range = PickupLightRange;
            light.ShadowType = PickupLightShadowType;

            var serial = pickup.Serial;
            PickupLights[serial] = light;
            PickupLightCoroutines[serial] = Timing.RunCoroutine(TrackPickupLight(pickup, light));

            return light;
        }
        catch (Exception ex)
        {
            Log.Error($"CItem.AddPickupLight failed ({GetType().Name}): {ex}");
            return null;
        }
    }

    /// <summary>Pickup の位置に毎フレーム Light を追従させる。</summary>
    private static IEnumerator<float> TrackPickupLight(Pickup pickup, Exiled.API.Features.Toys.Light light)
    {
        while (pickup != null && light != null
               && pickup.Base != null && light.Base != null
               && pickup.Base.gameObject != null && light.Base.gameObject != null)
        {
            light.Position = pickup.Position;
            yield return Timing.WaitForOneFrame;
        }
    }

    /// <summary>指定 Pickup のライトを削除する。</summary>
    public virtual bool RemovePickupLight(Pickup? pickup)
    {
        if (pickup == null) return false;
        return DestroyPickupLightInternal(pickup.Serial);
    }

    /// <summary>内部用: シリアル指定でライトとコルーチンを破棄する。</summary>
    private static bool DestroyPickupLightInternal(ushort serial)
    {
        if (PickupLightCoroutines.TryGetValue(serial, out var handle))
        {
            Timing.KillCoroutines(handle);
            PickupLightCoroutines.Remove(serial);
        }

        if (!PickupLights.TryGetValue(serial, out var light))
            return false;

        try
        {
            if (light != null && light.Base != null && light.Base.gameObject != null)
                NetworkServer.Destroy(light.Base.gameObject);
        }
        catch (Exception ex)
        {
            Log.Error($"CItem.DestroyPickupLightInternal failed: {ex}");
        }

        return PickupLights.Remove(serial);
    }

    /// <summary>プレイヤーの所持品から一致する最初のインスタンスを消す。見つからなければ false。</summary>
    public bool RemoveFrom(Player? player, bool destroy = true)
    {
        if (player == null) return false;

        foreach (var item in player.Items.ToList())
        {
            if (item == null) continue;
            if (!Check(item)) continue;

            SerialToItem.Remove(item.Serial);
            if (destroy)
                player.RemoveItem(item, destroy: true);
            return true;
        }

        return false;
    }

    // ==== Check / TryGet ====

    /// <summary>このアイテムが CItem のこのインスタンス由来か。
    /// Hybrid の sub インスタンスから呼んだ場合、その serial の現在アクティブな sub が
    /// 自分であれば true を返す。</summary>
    public bool Check(Item? item)
    {
        if (item == null) return false;
        if (!SerialToItem.TryGetValue(item.Serial, out var ci) || ci == null) return false;
        if (ReferenceEquals(ci, this)) return true;
        return ci is CItemHybrid hybrid && hybrid.IsCurrentSub(item.Serial, this);
    }

    /// <summary>このピックアップが CItem のこのインスタンス由来か。
    /// Hybrid の sub インスタンスから呼んだ場合、その serial の現在アクティブな sub が
    /// 自分であれば true を返す。</summary>
    public bool Check(Pickup? pickup)
    {
        if (pickup == null) return false;
        if (!SerialToItem.TryGetValue(pickup.Serial, out var ci) || ci == null) return false;
        if (ReferenceEquals(ci, this)) return true;
        return ci is CItemHybrid hybrid && hybrid.IsCurrentSub(pickup.Serial, this);
    }

    /// <summary>プレイヤーが手に持っている現在のアイテムがこの CItem のインスタンスか。</summary>
    public bool CheckHeld(Player? player)
        => player != null && Check(player.CurrentItem);

    /// <summary>プレイヤーがこの CItem をインベントリに所持しているか。</summary>
    public bool HasIn(Player? player)
    {
        if (player == null) return false;
        foreach (var it in player.Items)
            if (Check(it)) return true;
        return false;
    }

    /// <summary>シリアル→登録済み CItem。無ければ false。</summary>
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

    public static T? Get<T>() where T : CItem
        => RegisteredInstances.OfType<T>().FirstOrDefault();

    // ==== 派生クラス向けイベントフック ====

    /// <summary>
    /// プレイヤーのインベントリにこの CItem が入った瞬間（経路問わず）発火する
    /// 汎用フック。Give / 床からのピックアップ / ロードアウト / SCP-914 /
    /// 他プラグイン経由の AddItem など全ての経路で一度だけ呼ばれる。
    /// </summary>
    /// <param name="ev">Exiled の ItemAddedEventArgs。Pickup が床由来の場合は ev.Pickup に元ピックアップが入る。</param>
    /// <param name="displayMessage">Give(displayMessage:false) が明示されなければ true。</param>
    protected virtual void OnAcquired(PlayerEvents.ItemAddedEventArgs ev, bool displayMessage) { }

    protected virtual void OnReleased(PlayerEvents.ItemRemovedEventArgs ev) { }

    protected virtual void OnSpawned(Pickup pickup) { }

    protected virtual void OnPickingUp(PlayerEvents.PickingUpItemEventArgs ev) { }
    protected virtual void OnDropping(PlayerEvents.DroppingItemEventArgs ev) { }
    protected virtual void OnUsing(PlayerEvents.UsingItemEventArgs ev) { }
    protected virtual void OnUsed(PlayerEvents.UsedItemEventArgs ev) { }
    protected virtual void OnShooting(PlayerEvents.ShootingEventArgs ev) { }
    protected virtual void OnShot(PlayerEvents.ShotEventArgs ev) { }
    protected virtual void OnHurtingOthers(PlayerEvents.HurtingEventArgs ev) { }
    protected virtual void OnOwnerDying(PlayerEvents.DyingEventArgs ev) { }
    protected virtual void OnChangingItem(PlayerEvents.ChangingItemEventArgs ev) { }
    protected virtual void OnThrowingRequest(PlayerEvents.ThrowingRequestEventArgs ev) { }
    protected virtual void OnPickupAdded(MapEvents.PickupAddedEventArgs ev) { }
    protected virtual void OnPickupDestroyed(MapEvents.PickupDestroyedEventArgs ev) { }
    protected virtual void OnWaitingForPlayers() { }

    /// <summary>
    /// SCP-914 が床に置かれたこの CItem の Pickup をアップグレードするとき。
    /// 変換ルールは <see cref="Scp914Changes"/> で一元管理しており、
    /// このフックは「Registry にルールが無い場合の最終防衛」として
    /// 出力位置にテレポート (Keep) するデフォルト挙動だけを持つ。
    /// </summary>
    protected virtual void OnUpgradingPickup(Scp914Events.UpgradingPickupEventArgs ev)
    {
        ev.IsAllowed = false;
        if (ev.Pickup != null)
            ev.Pickup.Position = ev.OutputPosition;
    }

    /// <summary>
    /// SCP-914 がインベントリ内のこの CItem をアップグレードするとき。
    /// 変換ルールは <see cref="Scp914Changes"/> で一元管理しており、
    /// このフックは Registry ヒット無しの場合のデフォルト (CItem 保持) を担当する。
    /// </summary>
    protected virtual void OnUpgradingInventoryItem(Scp914Events.UpgradingInventoryItemEventArgs ev)
    {
        ev.IsAllowed = false;
    }

    // ==== 静的イベントディスパッチ ====

    private static void Dispatch(ushort serial, Action<CItem> body, string tag)
    {
        if (!SerialToItem.TryGetValue(serial, out var ci) || ci == null) return;
        try { body(ci); }
        catch (Exception ex) { Log.Error($"CItem.{tag} error in {ci.GetType().Name}: {ex}"); }
    }

    private static void OnAnyPickingUpItem(PlayerEvents.PickingUpItemEventArgs ev)
    {
        // 床からの手動ピックアップ特有のキャンセル用フック。
        // Hint 表示は OnAnyItemAdded 側に集約したのでここでは扱わない。
        if (ev?.Pickup == null) return;
        Dispatch(ev.Pickup.Serial, ci => ci.OnPickingUp(ev), nameof(OnPickingUp));
    }

    /// <summary>
    /// 全ての経路で「プレイヤーのインベントリに ItemBase が足された瞬間」を拾う。
    /// Give() 由来 / 床拾い / ロードアウト / SCP-914 / 他プラグイン AddItem すべて。
    /// </summary>
    private static void OnAnyItemAdded(PlayerEvents.ItemAddedEventArgs ev)
    {
        if (ev?.Player == null || ev.Item == null) return;

        CItem? ci;
        bool displayMessage;

        // 1. Give() からの呼び出し: pending marker で CItem を決定し、即 tracking
        if (_pendingGiveCItem != null)
        {
            ci = _pendingGiveCItem;
            displayMessage = _pendingGiveDisplayMessage;
            SerialToItem[ev.Item.Serial] = ci;
            _pendingGiveCItem = null;
            _pendingGiveDisplayMessage = false;
        }
        // 2. それ以外 (床拾い / 914 / 他プラグインの AddItem): 既にシリアル追跡済みなら CItem
        else if (SerialToItem.TryGetValue(ev.Item.Serial, out var existing) && existing != null)
        {
            ci = existing;
            displayMessage = true;
        }
        else
        {
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
        // ItemRemoved は「インベントリから抜けた」時点の通知。
        // 対応する Pickup に遷移している可能性があるので SerialToItem は掃除しない。
        // 本当に消滅したときは OnAnyPickupDestroyed 側で掃除する。
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

        // 所持してた CItem 全てに通知
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

        // 旧アイテム: ChangingItem は「変更前」に発火するので Player.CurrentItem が旧側。
        // CItem を手放す瞬間なら Hint を空文字で上書きして掃除する。
        var oldItem = ev.Player.CurrentItem;
        if (oldItem != null
            && SerialToItem.TryGetValue(oldItem.Serial, out var oldCi)
            && oldCi != null)
        {
            try { ev.Player.ShowHint(string.Empty, 0.1f); }
            catch (Exception e) { Log.Error($"CItem.ChangingItem(clear hint) error in {oldCi.GetType().Name}: {e}"); }
        }

        // 新アイテム側: CItem なら派生フック + selected hint
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
        var item = ev?.Item;
        if (item == null) return;
        Dispatch(item.Serial, ci => ci.OnThrowingRequest(ev!), nameof(OnThrowingRequest));
    }

    private static void OnAnyPickupAdded(MapEvents.PickupAddedEventArgs ev)
    {
        if (ev?.Pickup == null) return;

        CItem? ci;

        // 1. Spawn() 由来: _pendingSpawnCItem マーカーで CItem を解決し SerialToItem に登録
        if (_pendingSpawnCItem != null)
        {
            ci = _pendingSpawnCItem;
            SerialToItem[ev.Pickup.Serial] = ci;
        }
        // 2. 既に追跡中の Pickup (プレイヤーのドロップ等)
        else if (SerialToItem.TryGetValue(ev.Pickup.Serial, out var existing) && existing != null)
        {
            ci = existing;
        }
        else
        {
            return;
        }

        try { ci.OnPickupAdded(ev); }
        catch (Exception ex) { Log.Error($"CItem.OnPickupAdded error in {ci.GetType().Name}: {ex}"); }

        // PickupLightEnabled が true の CItem は自動でライトを追加。
        if (ci.PickupLightEnabled)
            ci.AddPickupLight(ev.Pickup);

        // PickupSchematicName が設定されている CItem は schematic を貼り付けて追従。
        if (!string.IsNullOrEmpty(ci.PickupSchematicName))
            AttachPickupSchematic(ev.Pickup, ci.PickupSchematicName!);
    }

    private static void OnAnyPickupDestroyed(MapEvents.PickupDestroyedEventArgs ev)
    {
        if (ev?.Pickup == null) return;

        if (!SerialToItem.TryGetValue(ev.Pickup.Serial, out var ci) || ci == null) return;

        try { ci.OnPickupDestroyed(ev); }
        catch (Exception ex) { Log.Error($"CItem.OnPickupDestroyed error in {ci.GetType().Name}: {ex}"); }

        // Pickup ライトとコルーチンの破棄
        DestroyPickupLightInternal(ev.Pickup.Serial);

        // 追従中の schematic も破棄。
        DestroyPickupSchematicInternal(ev.Pickup.Serial);

        // PickupDestroyed は「プレイヤーが拾って pickup が消えた」時にも発火するため、
        // ここで SerialToItem を即削除すると同じ CItem が拾われた瞬間に追跡が切れる。
        // シリアル再利用は round 毎なので WaitingForPlayers 側で一括掃除に任せる。
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
        SerialToItem.Clear();

        // 残存する Pickup ライトとコルーチンを一括破棄
        foreach (var serial in PickupLights.Keys.ToList())
            DestroyPickupLightInternal(serial);
        PickupLights.Clear();
        PickupLightCoroutines.Clear();

        // 残存する Pickup schematic も同様に一括破棄
        foreach (var serial in PickupSchematics.Keys.ToList())
            DestroyPickupSchematicInternal(serial);
        PickupSchematics.Clear();
        PickupSchematicCoroutines.Clear();

        foreach (var ci in RegisteredInstances.ToList())
        {
            try { ci.OnWaitingForPlayers(); }
            catch (Exception ex) { Log.Error($"CItem.OnWaitingForPlayers error in {ci.GetType().Name}: {ex}"); }
        }
    }

    // ==== Pickup schematic 追従 ====

    private static void AttachPickupSchematic(Pickup pickup, string schematicName)
    {
        if (PickupSchematics.ContainsKey(pickup.Serial)) return;

        try
        {
            var schem = ObjectSpawner.SpawnSchematic(schematicName, pickup.Position, pickup.Rotation);
            if (schem == null) return;

            PickupSchematics[pickup.Serial] = schem;
            PickupSchematicCoroutines[pickup.Serial] = Timing.RunCoroutine(TrackPickupSchematic(pickup, schem));
        }
        catch (Exception ex)
        {
            Log.Error($"CItem.AttachPickupSchematic({schematicName}) failed: {ex}");
        }
    }

    private static IEnumerator<float> TrackPickupSchematic(Pickup pickup, SchematicObject schem)
    {
        while (pickup != null && schem != null
               && pickup.Base != null && schem.gameObject != null)
        {
            schem.transform.position = pickup.Position;
            schem.transform.rotation = pickup.Rotation;
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

        if (PickupSchematics.TryGetValue(serial, out var schem))
        {
            try { schem?.Destroy(); }
            catch (Exception ex) { Log.Warn($"CItem.DestroyPickupSchematicInternal failed: {ex}"); }
            PickupSchematics.Remove(serial);
        }
    }

    // ==== Goggles (Scp1344) ライフサイクル ディスパッチ ====

    private static void OnAnyScp1344ChangedStatus(Scp1344Events.ChangedStatusEventArgs ev)
    {
        if (ev?.Item == null) return;
        if (!SerialToItem.TryGetValue(ev.Item.Serial, out var ci) || ci == null) return;
        if (!ci.IsGoggles) return;

        try
        {
            switch (ev.Scp1344Status)
            {
                case Scp1344Status.Activating:
                    // 装着開始: WearingTime に応じて _useTime を縮める
                    ev.Scp1344.Base._useTime = 5f - ci.WearingTime;
                    break;

                case Scp1344Status.Active:
                    // 装着完了
                    if (ci.Remove1344Effect)
                    {
                        ev.Player.DisableEffect(EffectType.Scp1344);
                        ev.Player.ReferenceHub.EnableWearables(WearableElements.Scp1344Goggles);
                    }
                    ci.OnGogglesWorn(ev.Player, ev.Scp1344);
                    break;

                case Scp1344Status.Deactivating:
                    // 取り外し開始: RemovingTime に応じて _useTime を縮める
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
        if (!SerialToItem.TryGetValue(ev.Item.Serial, out var ci) || ci == null) return;
        if (!ci.IsGoggles) return;

        // Deactivating → Idle (取り外し完了) のとき OnGogglesRemoved を発火。
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
        if (!SerialToItem.TryGetValue(ev.Item.Serial, out var ci) || ci == null) return;
        if (!ci.IsGoggles) return;
        if (!ci.CanBeRemoveSafely) return;

        // 安全取り外し可: Deactivate イベントは「即座に Idle にする」形ではなく、
        // ChangingStatus 経由で Deactivating → Idle 遷移を回す。
        ev.NewStatus = Scp1344Status.Idle;
        ev.IsAllowed = false;
    }

    // ==== SerialTracker (CItemHybrid から serial の dispatch先を差し替えるための公開ヘルパー) ====

    public static class SerialTracker
    {
        /// <summary>指定 serial の dispatch先を強制的に差し替える。イベント購読は行わない。</summary>
        public static void ForceRegister(ushort serial, CItem item)
        {
            if (item == null) return;
            SerialToItem[serial] = item;
        }

        /// <summary>指定 serial の dispatch登録を解除する。</summary>
        public static void ForceUnregister(ushort serial)
            => SerialToItem.Remove(serial);

        public static bool TryGet(ushort serial, out CItem? item)
            => SerialToItem.TryGetValue(serial, out item!);
    }

    // ==== internal shims (CItemHybrid が sub インスタンスの protected メソッドを呼ぶために使う) ====

    internal ItemType GetBaseItem() => BaseItem;

    internal void CallOnAcquired(PlayerEvents.ItemAddedEventArgs ev, bool displayMessage)
        => OnAcquired(ev, displayMessage);
    internal void CallOnReleased(PlayerEvents.ItemRemovedEventArgs ev) => OnReleased(ev);
    internal void CallOnSpawned(Pickup pickup) => OnSpawned(pickup);
    internal void CallOnPickingUp(PlayerEvents.PickingUpItemEventArgs ev) => OnPickingUp(ev);
    internal void CallOnDropping(PlayerEvents.DroppingItemEventArgs ev) => OnDropping(ev);
    internal void CallOnUsing(PlayerEvents.UsingItemEventArgs ev) => OnUsing(ev);
    internal void CallOnUsed(PlayerEvents.UsedItemEventArgs ev) => OnUsed(ev);
    internal void CallOnShooting(PlayerEvents.ShootingEventArgs ev) => OnShooting(ev);
    internal void CallOnShot(PlayerEvents.ShotEventArgs ev) => OnShot(ev);
    internal void CallOnHurtingOthers(PlayerEvents.HurtingEventArgs ev) => OnHurtingOthers(ev);
    internal void CallOnOwnerDying(PlayerEvents.DyingEventArgs ev) => OnOwnerDying(ev);
    internal void CallOnChangingItem(PlayerEvents.ChangingItemEventArgs ev) => OnChangingItem(ev);
    internal void CallOnThrowingRequest(PlayerEvents.ThrowingRequestEventArgs ev) => OnThrowingRequest(ev);
    internal void CallOnPickupAdded(MapEvents.PickupAddedEventArgs ev) => OnPickupAdded(ev);
    internal void CallOnPickupDestroyed(MapEvents.PickupDestroyedEventArgs ev) => OnPickupDestroyed(ev);
    internal void CallOnWaitingForPlayers() => OnWaitingForPlayers();
    internal void CallOnUpgradingPickup(Scp914Events.UpgradingPickupEventArgs ev) => OnUpgradingPickup(ev);
    internal void CallOnUpgradingInventoryItem(Scp914Events.UpgradingInventoryItemEventArgs ev)
        => OnUpgradingInventoryItem(ev);
    internal void CallCustomizeItem(Item item) => CustomizeItem(item);

    // ==== Give() pending state を CItemHybrid から制御するための内部 API ====

    internal static void SetPendingGive(CItem ci, bool displayMessage)
    {
        _pendingGiveCItem = ci;
        _pendingGiveDisplayMessage = displayMessage;
    }

    internal static void ClearPendingGive()
    {
        _pendingGiveCItem = null;
        _pendingGiveDisplayMessage = false;
    }
}
