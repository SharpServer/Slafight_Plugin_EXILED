using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using AdminToys;
using Exiled.API.Features;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using MEC;
using Mirror;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Interface;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Features;

public enum ObjectPrefabTagSearchMode
{
    All,
    ExactType,
    AssignableType,
}

public abstract class ObjectPrefab : IObjectPrefab
{
    private static readonly Dictionary<Type, PropertyInfo[]> AutomaticOptionProperties = new();
    private static readonly Dictionary<Type, MemberInfo[]> DeclaredOptionMembers = new();
    private readonly List<InteractableHandle> _interactables = [];
    private readonly Dictionary<string, SchematicBlock> _managedBlocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CoroutineHandle> _scheduledCallbacks = [];
    private Vector3 _position = Vector3.zero;
    private Quaternion _rotation = Quaternion.identity;
    private Vector3 _scale = Vector3.one;
    private string _objectInstanceID = string.Empty;
    private string _tag = string.Empty;

    // Destroy 冪等化用
    private bool _isDestroyed;

    public string ObjectInstanceID
    {
        get => _objectInstanceID;
        set
        {
            if (!string.IsNullOrEmpty(_objectInstanceID))
                throw new InvalidOperationException("ObjectInstanceID can only be set once.");
            _objectInstanceID = value[..Math.Min(5, value.Length)];
        }
    }

    public virtual Vector3 Position
    {
        get => _position;
        set
        {
            _position = value;
            OnTransformUpdated();
        }
    }

    public virtual Quaternion Rotation
    {
        get => _rotation;
        set
        {
            _rotation = value;
            OnTransformUpdated();
        }
    }

    public virtual Vector3 Scale
    {
        get => _scale;
        set
        {
            _scale = value;
            OnTransformUpdated();
        }
    }

    public virtual bool AutoDestroyEnabled { get; set; } = false;
    public virtual float AutoDestroyTime { get; set; } = -1f;
    public virtual CoroutineHandle AutoDestroyCoroutineHandle { get; set; } = new CoroutineHandle();
    public virtual bool FollowMarkerTransform => true;
    public virtual bool IsSaveable { get; set; } = true;
    public virtual string Tag
    {
        get => _tag;
        set => _tag = value?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Save/Load 用の MaxRooms。デフォルト 1。
    /// </summary>
    public virtual int MaxRooms { get; set; } = 1;

    public virtual float ToySearchRadius { get; set; } = 0f;

    /// <summary>
    /// この Prefab が管理しているスキマティック。
    /// <see cref="SchematicName"/> を宣言していれば Create 時に自動でスポーンされる。
    /// </summary>
    public SchematicObject? Schematic { get; private set; }

    /// <summary>この Prefab が管理している Interactable のハンドル一覧。</summary>
    public IReadOnlyList<InteractableHandle> Interactables => _interactables;

    /// <summary>managed Interactable を 1 つ以上持つか（半径フォールバック対象の判定に使う）。</summary>
    public bool HasInteractables => _interactables.Count > 0;

    /// <summary>
    /// Schematic 内で ObjectPrefabSchematicInfo により採用されたキーのスナップショット。
    /// </summary>
    public IReadOnlyCollection<string> ManagedBlockKeys => _managedBlocks.Keys.ToArray();

    /// <summary>
    /// 宣言すると Create 時に自動でスキマティックをスポーンする。
    /// null / 空文字なら何もスポーンしない（動的に切り替える場合は getter で分岐してよい）。
    /// </summary>
    protected virtual string? SchematicName => null;

    /// <summary>
    /// Create から <see cref="OnSetup"/> 呼び出しまでの遅延秒数。0 以下なら即時。
    /// </summary>
    protected virtual float SetupDelay => 0.5f;

    public virtual ObjectPrefab Create()
    {
        Log.Debug($"[ObjectPrefab]{GetType().Name} Create Invoked.");
        ObjectInstanceID = InstanceManager.GenerateUniqueId();
        InstanceManager.Register(this);
        if (AutoDestroyEnabled && AutoDestroyTime > 0f)
            AutoDestroyCoroutineHandle = Timing.RunCoroutine(AutoDestroy());

        string? schematicName = SchematicName;
        if (!string.IsNullOrEmpty(schematicName))
            SpawnManagedSchematic(schematicName!);

        OnCreate();

        if (SetupDelay > 0f)
            ScheduleDelayed(SetupDelay, InvokeSetup);
        else
            InvokeSetup();

        return this;
    }

    private void InvokeSetup()
    {
        if (_isDestroyed)
            return;

        try
        {
            OnSetup();
        }
        catch (Exception e)
        {
            Log.Error($"[ObjectPrefab]{GetType().Name} OnSetup exception: {e}");
        }
    }

    public virtual void Destroy()
    {
        // すでに Destroy 済みなら何もしない
        if (_isDestroyed)
            return;

        _isDestroyed = true;

        Log.Debug($"[ObjectPrefab]{GetType().Name} Destroy Invoked.");

        if (!string.IsNullOrEmpty(ObjectInstanceID))
            InstanceManager.Unregister(ObjectInstanceID);

        if (AutoDestroyCoroutineHandle.IsRunning)
            Timing.KillCoroutines(AutoDestroyCoroutineHandle);

        KillScheduledCallbacks();

        try
        {
            OnDestroy();
        }
        catch (Exception e)
        {
            Log.Error($"[ObjectPrefab]{GetType().Name} OnDestroy exception: {e}");
        }

        DestroyManagedInteractables();
        DestroyManagedSchematic();
    }

    protected virtual void OnCreate() { }

    /// <summary>
    /// Create から <see cref="SetupDelay"/> 経過後に 1 回呼ばれる。
    /// Interactable の生成やアニメーションの初期化はここで行う。
    /// </summary>
    protected virtual void OnSetup() { }

    protected virtual void OnDestroy() { }
    protected virtual void OnTransformUpdated() => SyncManagedObjects();

    protected SchematicObject? SpawnManagedSchematic(string schematicName, bool applyScale = true)
    {
        DestroyManagedSchematic();
        Schematic = ObjectSpawner.SpawnSchematic(schematicName, Position, Rotation);
        if (Schematic != null && applyScale)
            Schematic.Scale = Scale;

        AdoptSchematicBlocks();
        SyncManagedObjects();
        return Schematic;
    }

    protected void SetManagedSchematic(SchematicObject? schematic, bool destroyPrevious = true, bool applyScale = true)
    {
        if (destroyPrevious)
            DestroyManagedSchematic();

        Schematic = schematic;
        if (Schematic != null && applyScale)
            Schematic.Scale = Scale;

        AdoptSchematicBlocks();
        SyncManagedObjects();
    }

    protected void DestroyManagedSchematic()
    {
        ReleaseAdoptedBlocks();

        var schematic = Schematic;
        Schematic = null;

        if (schematic == null)
            return;

        try
        {
            // 安全な Destroy を呼ぶ
            schematic.Destroy();
        }
        catch (Exception e)
        {
            Log.Error($"[ObjectPrefab]{GetType().Name} DestroyManagedSchematic exception: {e}");
        }
    }

    /// <summary>
    /// スキマティック内の ObjectPrefabSchematicInfo 付きブロックを管理下へ採用する。
    /// Interactable ブロックは <see cref="InteractableHandle"/> になり、
    /// その他のブロックは <see cref="GetBlock"/> でキー参照できる。
    /// </summary>
    private void AdoptSchematicBlocks()
    {
        ReleaseAdoptedBlocks();

        if (Schematic == null)
            return;

        foreach (SchematicBlock block in Schematic.GetPrefabManagedBlocks())
        {
            string key = block.ObjectPrefabKey;
            _managedBlocks[key] = block;

            if (!block.TryGetComponent(out InvisibleInteractableToy toyBase))
                continue;

            InteractableToy? toy = InteractableToy.Get(toyBase);
            if (toy == null)
            {
                Log.Warn($"[ObjectPrefab]{GetType().Name} adopted block '{key}' has an interactable without a LabAPI wrapper.");
                continue;
            }

            var handle = new InteractableHandle(
                this,
                toy,
                Vector3.zero,
                Vector3.one,
                key: key,
                ownsToy: false,
                syncTransform: false);

            _interactables.Add(handle);
            ObjectPrefabInteractionRouter.Register(handle);
        }
    }

    /// <summary>採用したブロック / Interactable を管理から外す（Toy 自体は破棄しない）。</summary>
    private void ReleaseAdoptedBlocks()
    {
        RemoveInteractableCore(handle => !handle.OwnsToy, destroyToy: false);
        _managedBlocks.Clear();
    }

    /// <summary>
    /// ObjectPrefabSchematicInfo のキーで採用済み Interactable を取得する（大文字小文字無視）。
    /// </summary>
    public InteractableHandle? GetInteractable(string key)
        => _interactables.FirstOrDefault(handle =>
            string.Equals(handle.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// ObjectPrefabSchematicInfo のキーで採用済みブロックを取得する（大文字小文字無視）。
    /// </summary>
    public SchematicBlock? GetBlock(string key)
        => _managedBlocks.TryGetValue(key, out SchematicBlock block) && block != null ? block : null;

    /// <summary>
    /// キー（ObjectPrefabSchematicInfo）またはブロック名からコンポーネントを取得する。
    /// キー一致を優先し、無ければブロック名で検索する。
    /// </summary>
    public T? GetBlockComponent<T>(string keyOrName) where T : UnityEngine.Component
    {
        SchematicBlock? block = GetBlock(keyOrName) ?? Schematic?.FindBlock(keyOrName);
        return block != null && block.TryGetComponent(out T component) ? component : null;
    }

    /// <summary>
    /// 採用ブロック（ObjectPrefabSchematicInfo キー）またはブロック名（完全一致）の
    /// ブロックをサブツリーごとネットワーク破棄する。
    /// 配下に採用 Interactable があれば自動で管理から外れる（Toy はブロックと一緒に破棄）。
    /// </summary>
    protected bool DestroyBlock(string keyOrName)
    {
        SchematicBlock? block = GetBlock(keyOrName) ?? Schematic?.FindBlock(keyOrName, allowPartial: false);
        if (block == null)
            return false;

        Transform root = block.transform;

        RemoveInteractableCore(
            handle => !handle.OwnsToy && !handle.Toy.IsDestroyed && handle.Toy.Transform.IsChildOf(root),
            destroyToy: false);

        foreach (string managedKey in _managedBlocks
                     .Where(pair => pair.Value == null || pair.Value.transform.IsChildOf(root))
                     .Select(pair => pair.Key)
                     .ToList())
        {
            _managedBlocks.Remove(managedKey);
        }

        foreach (NetworkIdentity identity in block.GetComponentsInChildren<NetworkIdentity>(true))
        {
            if (identity != null && identity.isServer)
                NetworkServer.Destroy(identity.gameObject);
        }

        if (block != null && block.gameObject != null)
            UnityEngine.Object.Destroy(block.gameObject);

        return true;
    }

    /// <summary>
    /// 採用ブロック（ObjectPrefabSchematicInfo キー）またはブロック名（完全一致）の
    /// ブロックサブツリーのネットワーク可視状態を UnSpawn / Spawn で切り替える。
    /// サーバー側オブジェクトは保持されるため、何度でも切り替えられる。
    /// </summary>
    protected bool SetBlockSpawned(string keyOrName, bool spawned)
    {
        SchematicBlock? block = GetBlock(keyOrName) ?? Schematic?.FindBlock(keyOrName, allowPartial: false);
        if (block == null)
            return false;

        return SetBlockSpawned(block, spawned);
    }

    /// <summary>
    /// ObjectPrefabSchematicInfo で採用した親ブロック配下から、名前が完全一致する子ブロックを探して
    /// ネットワーク可視状態を切り替える。
    /// </summary>
    protected bool SetChildBlockSpawned(string parentKeyOrName, string childName, bool spawned)
    {
        SchematicBlock? parent = GetBlock(parentKeyOrName) ?? Schematic?.FindBlock(parentKeyOrName, allowPartial: false);
        if (parent == null || string.IsNullOrWhiteSpace(childName))
            return false;

        SchematicBlock? child = parent.GetComponentsInChildren<SchematicBlock>(true)
            .FirstOrDefault(candidate =>
                candidate != parent &&
                string.Equals(candidate.name, childName, StringComparison.OrdinalIgnoreCase));
        return child != null && SetBlockSpawned(child, spawned);
    }

    private static bool SetBlockSpawned(SchematicBlock block, bool spawned)
    {

        foreach (NetworkIdentity identity in block.GetComponentsInChildren<NetworkIdentity>(true))
        {
            if (identity == null)
                continue;

            bool isSpawned = identity.netId != 0;
            if (spawned == isSpawned)
                continue;

            if (spawned)
                NetworkServer.Spawn(identity.gameObject);
            else
                NetworkServer.UnSpawn(identity.gameObject);
        }

        return true;
    }

    /// <summary>
    /// managed Interactable を 1 つ生成して登録する。
    /// 返り値のハンドルで Interacting / Interacted を購読できる。
    /// 位置・スケールは Prefab（Schematic があればその transform）に自動追従する。
    /// </summary>
    protected InteractableHandle AddInteractable(
        float duration = 3f,
        Vector3? offset = null,
        Vector3? scale = null,
        InvisibleInteractableToy.ColliderShape shape = InvisibleInteractableToy.ColliderShape.Box,
        bool spawn = true)
    {
        var toy = InteractableToy.Create(networkSpawn: false);
        toy.InteractionDuration = duration;
        toy.Shape = shape;

        InteractableHandle handle = RegisterManagedInteractable(toy, offset ?? Vector3.zero, scale ?? Vector3.one);
        if (spawn)
            toy.Spawn();

        SyncManagedObjects();
        return handle;
    }

    /// <summary>
    /// 外部で生成した Toy を managed Interactable として登録する。
    /// </summary>
    protected InteractableHandle RegisterManagedInteractable(InteractableToy toy, Vector3 localOffset, Vector3 baseScale)
    {
        RemoveInteractableCore(i => ReferenceEquals(i.Toy, toy), destroyToy: false);

        var handle = new InteractableHandle(this, toy, localOffset, baseScale);
        _interactables.Add(handle);
        ObjectPrefabInteractionRouter.Register(handle);
        SyncManagedObjects();
        return handle;
    }

    protected void UnregisterManagedInteractable(InteractableHandle handle, bool destroy = false)
        => RemoveInteractableCore(i => ReferenceEquals(i, handle), destroy);

    protected void DestroyManagedInteractables()
        => RemoveInteractableCore(_ => true, destroyToy: true);

    private void RemoveInteractableCore(Func<InteractableHandle, bool> predicate, bool destroyToy)
    {
        for (int i = _interactables.Count - 1; i >= 0; i--)
        {
            InteractableHandle handle = _interactables[i];
            if (!predicate(handle))
                continue;

            ObjectPrefabInteractionRouter.Unregister(handle);
            _interactables.RemoveAt(i);

            // 採用分（OwnsToy=false）はスキマティック側が寿命を持つため破棄しない
            if (!destroyToy || !handle.OwnsToy)
                continue;

            try
            {
                if (!handle.Toy.IsDestroyed)
                    handle.Toy.Destroy();
            }
            catch (Exception e)
            {
                Log.Error($"[ObjectPrefab]{GetType().Name} interactable destroy exception: {e}");
            }
        }
    }

    protected CoroutineHandle ScheduleDelayed(float delay, Action callback)
    {
        CoroutineHandle handle = default;
        handle = Timing.CallDelayed(delay, () =>
        {
            _scheduledCallbacks.Remove(handle);

            // すでに Destroy 済み or インスタンス再登録済みでない場合は何もしない
            if (_isDestroyed || string.IsNullOrEmpty(ObjectInstanceID) || InstanceManager.Get(ObjectInstanceID) != this)
                return;

            callback();
        });

        _scheduledCallbacks.Add(handle);
        return handle;
    }

    /// <summary>
    /// Animatorが指定ステートへ入り、normalizedTimeが1以上になるか別ステートへ遷移するまで監視する。
    /// Animatorを取得できない、またはステートへ入れない場合のみfallbackDurationを使用する。
    /// </summary>
    protected CoroutineHandle ScheduleAfterAnimatorState(
        Animator? animator,
        string stateName,
        float fallbackDuration,
        Action callback,
        float maxWaitSeconds = 30f)
    {
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        CoroutineHandle handle = default;
        handle = Timing.RunCoroutine(WaitForAnimatorState(
            animator,
            stateName?.Trim() ?? string.Empty,
            Math.Max(0f, fallbackDuration),
            Math.Max(0.5f, maxWaitSeconds),
            () =>
            {
                _scheduledCallbacks.Remove(handle);
                callback();
            }));
        _scheduledCallbacks.Add(handle);
        return handle;
    }

    private IEnumerator<float> WaitForAnimatorState(
        Animator? animator,
        string stateName,
        float fallbackDuration,
        float maxWaitSeconds,
        Action callback)
    {
        const float PollInterval = 0.02f;
        const float StateEntryTimeout = 0.5f;
        float elapsed = 0f;
        bool enteredState = false;
        bool useFallback = animator == null || string.IsNullOrWhiteSpace(stateName);

        // Animator.Playの反映を待ってから現在ステートを取得する。
        yield return Timing.WaitForOneFrame;

        while (!useFallback && elapsed < maxWaitSeconds)
        {
            yield return Timing.WaitForSeconds(PollInterval);
            elapsed += PollInterval;

            if (_isDestroyed || string.IsNullOrEmpty(ObjectInstanceID) || InstanceManager.Get(ObjectInstanceID) != this)
                yield break;

            if (animator == null)
            {
                useFallback = !enteredState;
                break;
            }

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName(stateName))
            {
                enteredState = true;
                if (state.normalizedTime >= 1f && !animator.IsInTransition(0))
                    break;
            }
            else if (enteredState)
            {
                break;
            }
            else if (elapsed >= StateEntryTimeout)
            {
                useFallback = true;
                break;
            }
        }

        if (useFallback)
        {
            float remainingFallback = Math.Max(0f, fallbackDuration - elapsed);
            if (remainingFallback > 0f)
                yield return Timing.WaitForSeconds(remainingFallback);
        }

        if (!_isDestroyed && !string.IsNullOrEmpty(ObjectInstanceID) && InstanceManager.Get(ObjectInstanceID) == this)
            callback();
    }

    private void KillScheduledCallbacks()
    {
        foreach (var handle in _scheduledCallbacks)
        {
            if (handle.IsRunning)
                Timing.KillCoroutines(handle);
        }

        _scheduledCallbacks.Clear();
    }

    public void SyncManagedObjects()
    {
        if (_isDestroyed)
            return;

        if (Schematic != null)
        {
            Schematic.Position = Position;
            Schematic.Rotation = Rotation;
            Schematic.Scale = Scale;
        }

        var sourcePosition = Schematic?.Position ?? Position;
        var sourceRotation = Schematic?.Rotation ?? Rotation;

        foreach (var handle in _interactables)
        {
            // 採用分はスキマティックの親子関係で追従するため同期しない
            if (!handle.SyncTransform)
                continue;

            SyncInteractableTransform(
                handle.Toy,
                sourcePosition + sourceRotation * handle.LocalOffset,
                sourceRotation,
                Vector3.Scale(handle.BaseScale, Scale));
        }
    }

    private static void SyncInteractableTransform(InteractableToy toy, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (toy.IsDestroyed)
            return;

        toy.Transform.localPosition = position;
        toy.Transform.localRotation = rotation;
        toy.Transform.localScale = scale;

        toy.Base.NetworkPosition = toy.Transform.localPosition;
        toy.Base.NetworkRotation = toy.Transform.localRotation;
        toy.Base.NetworkScale = toy.Transform.localScale;
    }

    /// <summary>
    /// 半径フォールバック用の判定。managed Interactable を持つ Prefab は
    /// ルーター経由で直接ディスパッチされるため対象外。
    /// </summary>
    public bool MatchesSearchRadius(Vector3 toyPosition)
        => !HasInteractables && ToySearchRadius > 0f && Vector3.Distance(Position, toyPosition) <= ToySearchRadius;

    /// <summary>
    /// このインスタンスの実行時型を基準にTag検索する。
    /// ExactTypeは同じ具象型のみ、AssignableTypeは同じ型とその派生型を検索する。
    /// </summary>
    public IEnumerable<ObjectPrefab> GetByTag(
        string tag,
        ObjectPrefabTagSearchMode searchMode = ObjectPrefabTagSearchMode.ExactType)
        => InstanceManager.GetByTag(tag, searchMode, GetType());

    /// <summary>
    /// このインスタンスと同じ具象型からTag検索する。
    /// </summary>
    public IEnumerable<TPrefab> GetByTag<TPrefab>(string tag, bool includeDerivedTypes = true)
        where TPrefab : ObjectPrefab
        => InstanceManager.GetByTag<TPrefab>(tag, includeDerivedTypes);

    /// <summary>
    /// 派生Prefabで宣言されたpublic getter/setter付きプロパティと
    /// public な <see cref="Option"/> メンバーを自動収集する。
    /// 独自のキー名や変換処理が必要な場合はoverrideできる。
    /// </summary>
    public virtual Dictionary<string, string> CollectOptions()
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (PropertyInfo property in GetAutomaticOptionProperties())
        {
            try
            {
                if (TrySerializeOptionValue(property.GetValue(this), property.PropertyType, out string value))
                    options[property.Name] = value;
            }
            catch (Exception e)
            {
                Log.Warn($"[ObjectPrefab]{GetType().Name} failed to collect option '{property.Name}': {e.Message}");
            }
        }

        foreach (KeyValuePair<string, Option> declared in GetDeclaredOptions())
            options[declared.Key] = declared.Value.Serialize();

        return options;
    }

    /// <summary>
    /// 派生Prefabで宣言されたpublic getter/setter付きプロパティへオプションを自動適用する。
    /// 独自のキー名、互換エイリアス、副作用制御が必要な場合はoverrideできる。
    /// </summary>
    public virtual void ApplyOptions(Dictionary<string, string> options)
    {
        if (options == null || options.Count == 0)
            return;

        var properties = GetAutomaticOptionProperties()
            .ToDictionary(property => property.Name, StringComparer.OrdinalIgnoreCase);
        var declaredOptions = GetDeclaredOptions();

        foreach (KeyValuePair<string, string> option in options)
        {
            if (option.Key.Equals(nameof(Tag), StringComparison.OrdinalIgnoreCase))
            {
                Tag = option.Value;
                continue;
            }

            if (declaredOptions.TryGetValue(option.Key, out Option declared))
            {
                if (!declared.TryApply(option.Value, out string optionError))
                {
                    Log.Warn(
                        $"[ObjectPrefab]{GetType().Name} could not apply option " +
                        $"'{option.Key}={option.Value}': {optionError}");
                }

                continue;
            }

            if (!properties.TryGetValue(option.Key, out PropertyInfo property))
                continue;

            try
            {
                if (!TryDeserializeOptionValue(option.Value, property.PropertyType, out object value))
                {
                    Log.Warn(
                        $"[ObjectPrefab]{GetType().Name} could not parse option " +
                        $"'{option.Key}={option.Value}' as {property.PropertyType.Name}.");
                    continue;
                }

                property.SetValue(this, value);
            }
            catch (Exception e)
            {
                Log.Warn($"[ObjectPrefab]{GetType().Name} failed to apply option '{option.Key}': {e.Message}");
            }
        }
    }

    /// <summary>
    /// 自動Option化するプロパティを追加で絞り込むためのフック。
    /// </summary>
    protected virtual bool IsAutomaticOptionProperty(PropertyInfo property) => true;

    private PropertyInfo[] GetAutomaticOptionProperties()
    {
        Type prefabType = GetType();
        PropertyInfo[] candidates;

        lock (AutomaticOptionProperties)
        {
            if (!AutomaticOptionProperties.TryGetValue(prefabType, out candidates))
            {
                var properties = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);

                for (Type type = prefabType;
                     type != null && type != typeof(ObjectPrefab);
                     type = type.BaseType)
                {
                    foreach (PropertyInfo property in type.GetProperties(
                                 BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                    {
                        MethodInfo getter = property.GetGetMethod();
                        MethodInfo setter = property.GetSetMethod();
                        if (getter == null ||
                            setter == null ||
                            getter.IsStatic ||
                            setter.IsStatic ||
                            getter.GetBaseDefinition().DeclaringType == typeof(ObjectPrefab) ||
                            property.PropertyType == typeof(Option) ||
                            property.GetIndexParameters().Length != 0 ||
                            properties.ContainsKey(property.Name))
                        {
                            continue;
                        }

                        properties[property.Name] = property;
                    }
                }

                candidates = properties.Values
                    .OrderBy(property => property.Name, StringComparer.Ordinal)
                    .ToArray();
                AutomaticOptionProperties[prefabType] = candidates;
            }
        }

        return candidates.Where(IsAutomaticOptionProperty).ToArray();
    }

    /// <summary>
    /// public な <see cref="Option"/> プロパティ/フィールド（メンバー名 → インスタンス）を収集する。
    /// </summary>
    protected Dictionary<string, Option> GetDeclaredOptions()
    {
        Type prefabType = GetType();
        MemberInfo[] members;

        lock (DeclaredOptionMembers)
        {
            if (!DeclaredOptionMembers.TryGetValue(prefabType, out members))
            {
                var collected = new Dictionary<string, MemberInfo>(StringComparer.OrdinalIgnoreCase);

                for (Type type = prefabType;
                     type != null && type != typeof(ObjectPrefab);
                     type = type.BaseType)
                {
                    foreach (PropertyInfo property in type.GetProperties(
                                 BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                    {
                        if (property.PropertyType == typeof(Option) &&
                            property.GetGetMethod() is { IsStatic: false } &&
                            property.GetIndexParameters().Length == 0 &&
                            !collected.ContainsKey(property.Name))
                        {
                            collected[property.Name] = property;
                        }
                    }

                    foreach (FieldInfo field in type.GetFields(
                                 BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                    {
                        if (field.FieldType == typeof(Option) && !collected.ContainsKey(field.Name))
                            collected[field.Name] = field;
                    }
                }

                members = collected.Values
                    .OrderBy(member => member.Name, StringComparer.Ordinal)
                    .ToArray();
                DeclaredOptionMembers[prefabType] = members;
            }
        }

        var result = new Dictionary<string, Option>(StringComparer.OrdinalIgnoreCase);
        foreach (MemberInfo member in members)
        {
            Option? option = member switch
            {
                PropertyInfo property => property.GetValue(this) as Option,
                FieldInfo field => field.GetValue(this) as Option,
                _ => null,
            };

            if (option != null)
                result[member.Name] = option;
        }

        return result;
    }

    internal static bool TrySerializeOptionValue(object? value, Type declaredType, out string serialized)
    {
        if (value == null)
        {
            serialized = string.Empty;
            return !declaredType.IsValueType || Nullable.GetUnderlyingType(declaredType) != null;
        }

        Type valueType = Nullable.GetUnderlyingType(declaredType) ?? declaredType;
        switch (value)
        {
            case Vector2 vector2:
                serialized = FormatComponents(vector2.x, vector2.y);
                return true;
            case Vector3 vector3:
                serialized = FormatComponents(vector3.x, vector3.y, vector3.z);
                return true;
            case Vector4 vector4:
                serialized = FormatComponents(vector4.x, vector4.y, vector4.z, vector4.w);
                return true;
            case Quaternion quaternion:
                serialized = FormatComponents(quaternion.x, quaternion.y, quaternion.z, quaternion.w);
                return true;
            case Color color:
                serialized = FormatComponents(color.r, color.g, color.b, color.a);
                return true;
        }

        if (valueType == typeof(string) || valueType.IsEnum)
        {
            serialized = value.ToString();
            return true;
        }

        if (value is IFormattable formattable)
        {
            serialized = formattable.ToString(null, CultureInfo.InvariantCulture);
            return true;
        }

        TypeConverter converter = TypeDescriptor.GetConverter(valueType);
        if (converter.CanConvertTo(typeof(string)))
        {
            serialized = converter.ConvertToInvariantString(value);
            return serialized != null;
        }

        serialized = string.Empty;
        return false;
    }

    internal static bool TryDeserializeOptionValue(string serialized, Type declaredType, out object value)
    {
        Type nullableType = Nullable.GetUnderlyingType(declaredType);
        Type valueType = nullableType ?? declaredType;

        if (nullableType != null && string.IsNullOrEmpty(serialized))
        {
            value = null;
            return true;
        }

        if (valueType == typeof(string))
        {
            value = serialized;
            return true;
        }

        if (valueType.IsEnum)
        {
            try
            {
                value = Enum.Parse(valueType, serialized, true);
                return true;
            }
            catch
            {
                value = null;
                return false;
            }
        }

        if (valueType == typeof(Vector2) && TryParseComponents(serialized, 2, out float[] vector2))
        {
            value = new Vector2(vector2[0], vector2[1]);
            return true;
        }

        if (valueType == typeof(Vector3) && TryParseComponents(serialized, 3, out float[] vector3))
        {
            value = new Vector3(vector3[0], vector3[1], vector3[2]);
            return true;
        }

        if (valueType == typeof(Vector4) && TryParseComponents(serialized, 4, out float[] vector4))
        {
            value = new Vector4(vector4[0], vector4[1], vector4[2], vector4[3]);
            return true;
        }

        if (valueType == typeof(Quaternion) && TryParseComponents(serialized, 4, out float[] quaternion))
        {
            value = new Quaternion(quaternion[0], quaternion[1], quaternion[2], quaternion[3]);
            return true;
        }

        if (valueType == typeof(Color) && TryParseComponents(serialized, 4, out float[] color))
        {
            value = new Color(color[0], color[1], color[2], color[3]);
            return true;
        }

        try
        {
            TypeConverter converter = TypeDescriptor.GetConverter(valueType);
            if (converter.CanConvertFrom(typeof(string)))
            {
                value = converter.ConvertFromInvariantString(serialized);
                return value != null;
            }

            value = Convert.ChangeType(serialized, valueType, CultureInfo.InvariantCulture);
            return value != null;
        }
        catch
        {
            value = null;
            return false;
        }
    }

    private static string FormatComponents(params float[] values)
        => string.Join(",", values.Select(value => value.ToString("R", CultureInfo.InvariantCulture)));

    private static bool TryParseComponents(string value, int expectedCount, out float[] components)
    {
        string normalized = value
            .Trim()
            .Trim('(', ')', '[', ']')
            .Replace(" ", string.Empty);
        string[] parts = normalized.Split(',');

        components = new float[expectedCount];
        if (parts.Length != expectedCount)
            return false;

        for (int i = 0; i < expectedCount; i++)
        {
            if (!float.TryParse(
                    parts[i],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out components[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// modコマンドでサブクラス固有のサブコマンドを処理する。
    /// 処理した場合はtrueを返す。未処理ならfalseを返す。
    /// </summary>
    public virtual bool HandleModCommand(ArraySegment<string> args, out string response)
    {
        response = string.Empty;
        return false;
    }

    protected virtual IEnumerator<float> AutoDestroy()
    {
        yield return Timing.WaitForSeconds(AutoDestroyTime);
        Destroy();
    }

    // Invoke Prefab Events.
    protected virtual void OnToyInteractingNearby(PlayerSearchingToyEventArgs eventArgs) { }

    public void InvokeToyInteractingNearby(PlayerSearchingToyEventArgs eventArgs)
        => OnToyInteractingNearby(eventArgs);
    
    protected virtual void OnToyInteractedNearby(PlayerSearchedToyEventArgs eventArgs) { }

    public void InvokeToyInteractedNearby(PlayerSearchedToyEventArgs eventArgs)
        => OnToyInteractedNearby(eventArgs);
    
    protected virtual void OnRoundStarted() { }
    public void InvokeRoundStarted()
        => OnRoundStarted();

    protected virtual void OnRoundRestarting()
    {
        Destroy();
    }
    public void InvokeRoundRestarting()
        => OnRoundRestarting();
}

public static class InstanceManager
{
    private static readonly Dictionary<string, ObjectPrefab> _instances = new();

    /// <summary>
    /// 既存インスタンスと衝突しない 5 文字の InstanceID を生成する。
    /// </summary>
    public static string GenerateUniqueId()
    {
        string id;
        do
        {
            id = Guid.NewGuid().ToString("N")[..5];
        } while (_instances.ContainsKey(id));

        return id;
    }

    public static void Register(ObjectPrefab prefab)
    {
        if (string.IsNullOrEmpty(prefab.ObjectInstanceID))
            throw new ArgumentException("ObjectInstanceID must be set before registering.");

        _instances[prefab.ObjectInstanceID] = prefab;
    }

    public static void Unregister(string objectInstanceID)
    {
        _instances.Remove(objectInstanceID);
    }

    public static ObjectPrefab? Get(string objectInstanceID)
    {
        return _instances.TryGetValue(objectInstanceID, out var prefab) ? prefab : null;
    }

    public static IEnumerable<ObjectPrefab> GetByTag(string tag)
        => GetByTag(tag, ObjectPrefabTagSearchMode.All);

    public static IEnumerable<ObjectPrefab> GetByTag(
        string tag,
        ObjectPrefabTagSearchMode searchMode,
        Type? referenceType = null)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return [];

        if (searchMode != ObjectPrefabTagSearchMode.All)
        {
            if (referenceType == null)
                throw new ArgumentNullException(nameof(referenceType), "A reference type is required for typed tag searches.");

            if (!typeof(ObjectPrefab).IsAssignableFrom(referenceType))
                throw new ArgumentException($"{referenceType.FullName} is not an ObjectPrefab type.", nameof(referenceType));
        }

        string normalizedTag = tag.Trim();
        return _instances.Values
            .Where(prefab =>
                string.Equals(prefab.Tag, normalizedTag, StringComparison.OrdinalIgnoreCase) &&
                MatchesSearchType(prefab.GetType(), referenceType, searchMode))
            .ToList();
    }

    public static IEnumerable<ObjectPrefab> GetByTag(
        string tag,
        Type prefabType,
        bool includeDerivedTypes = true)
        => GetByTag(
            tag,
            includeDerivedTypes
                ? ObjectPrefabTagSearchMode.AssignableType
                : ObjectPrefabTagSearchMode.ExactType,
            prefabType);

    public static IEnumerable<TPrefab> GetByTag<TPrefab>(
        string tag,
        bool includeDerivedTypes = true)
        where TPrefab : ObjectPrefab
    {
        Type referenceType = typeof(TPrefab);
        return GetByTag(tag, referenceType, includeDerivedTypes).Cast<TPrefab>();
    }

    private static bool MatchesSearchType(
        Type prefabType,
        Type? referenceType,
        ObjectPrefabTagSearchMode searchMode)
    {
        switch (searchMode)
        {
            case ObjectPrefabTagSearchMode.All:
                return true;
            case ObjectPrefabTagSearchMode.ExactType:
                return prefabType == referenceType;
            case ObjectPrefabTagSearchMode.AssignableType:
                return referenceType!.IsAssignableFrom(prefabType);
            default:
                throw new ArgumentOutOfRangeException(nameof(searchMode), searchMode, null);
        }
    }

    public static IEnumerable<ObjectPrefab> GetAll()
    {
        return _instances.Values.ToList();
    }

    public static void ClearAll()
    {
        foreach (var prefab in _instances.Values.ToList())
            prefab.Destroy();

        _instances.Clear();
    }

    public static void DestroyAll()
    {
        var instances = GetAll().ToList();
        foreach (var instance in instances)
            instance.Destroy();
    }
}
