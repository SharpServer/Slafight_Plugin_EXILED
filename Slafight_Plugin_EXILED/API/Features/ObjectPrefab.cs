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
    private readonly List<ManagedInteractableToy> _managedInteractables = [];
    private readonly List<CoroutineHandle> _scheduledCallbacks = [];
    private Vector3 _position = Vector3.zero;
    private Quaternion _rotation = Quaternion.identity;
    private Vector3 _scale = Vector3.one;
    private string _objectInstanceID = string.Empty;
    private string _tag = string.Empty;
    private SchematicObject? _managedSchematic;

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
    public SchematicObject? ManagedSchematic => _managedSchematic;
    public IReadOnlyCollection<InteractableToy> ManagedInteractables => _managedInteractables.Select(i => i.Toy).ToList();

    public virtual ObjectPrefab Create()
    {
        Log.Debug($"[ObjectPrefab]{GetType().Name} Create Invoked.");
        ObjectInstanceID = Guid.NewGuid().ToString("N")[..5];
        InstanceManager.Register(this);
        if (AutoDestroyEnabled && AutoDestroyTime > 0f)
            AutoDestroyCoroutineHandle = Timing.RunCoroutine(AutoDestroy());
        OnCreate();
        return this;
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
    protected virtual void OnDestroy() { }
    protected virtual void OnTransformUpdated() => SyncManagedObjects();

    protected SchematicObject? SpawnManagedSchematic(string schematicName, bool applyScale = true)
    {
        DestroyManagedSchematic();
        _managedSchematic = ObjectSpawner.SpawnSchematic(schematicName, Position, Rotation);
        if (_managedSchematic != null && applyScale)
            _managedSchematic.Scale = Scale;

        SyncManagedObjects();
        return _managedSchematic;
    }

    protected void SetManagedSchematic(SchematicObject? schematic, bool destroyPrevious = true, bool applyScale = true)
    {
        if (destroyPrevious)
            DestroyManagedSchematic();

        _managedSchematic = schematic;
        if (_managedSchematic != null && applyScale)
            _managedSchematic.Scale = Scale;

        SyncManagedObjects();
    }

    protected void DestroyManagedSchematic()
    {
        var schematic = _managedSchematic;
        _managedSchematic = null;

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

    protected InteractableToy CreateManagedInteractable(
        float interactionDuration = 3f,
        InvisibleInteractableToy.ColliderShape shape = InvisibleInteractableToy.ColliderShape.Box,
        Vector3? localOffset = null,
        Vector3? baseScale = null,
        bool spawn = true)
    {
        var toy = InteractableToy.Create(networkSpawn: false);
        toy.InteractionDuration = interactionDuration;
        toy.Shape = shape;

        RegisterManagedInteractable(toy, localOffset ?? Vector3.zero, baseScale ?? Vector3.one);
        if (spawn)
            toy.Spawn();

        SyncManagedObjects();
        return toy;
    }

    protected void RegisterManagedInteractable(InteractableToy toy, Vector3 localOffset, Vector3 baseScale)
    {
        _managedInteractables.RemoveAll(i => ReferenceEquals(i.Toy, toy));
        _managedInteractables.Add(new ManagedInteractableToy(toy, localOffset, baseScale));
        SyncManagedObjects();
    }

    protected void UnregisterManagedInteractable(InteractableToy toy, bool destroy = false)
    {
        _managedInteractables.RemoveAll(i =>
        {
            if (!ReferenceEquals(i.Toy, toy))
                return false;

            if (destroy && !i.Toy.IsDestroyed)
                i.Toy.Destroy();
            return true;
        });
    }

    protected void DestroyManagedInteractables()
    {
        foreach (var interactable in _managedInteractables.ToList())
        {
            try
            {
                if (!interactable.Toy.IsDestroyed)
                    interactable.Toy.Destroy();
            }
            catch (Exception e)
            {
                Log.Error($"[ObjectPrefab]{GetType().Name} DestroyManagedInteractables exception: {e}");
            }
        }

        _managedInteractables.Clear();
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

        if (_managedSchematic != null)
        {
            _managedSchematic.Position = Position;
            _managedSchematic.Rotation = Rotation;
            _managedSchematic.Scale = Scale;
        }

        var sourcePosition = _managedSchematic?.Position ?? Position;
        var sourceRotation = _managedSchematic?.Rotation ?? Rotation;

        foreach (var interactable in _managedInteractables)
        {
            SyncInteractableTransform(
                interactable.Toy,
                sourcePosition + sourceRotation * interactable.LocalOffset,
                sourceRotation,
                Vector3.Scale(interactable.BaseScale, Scale));
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

    public virtual bool MatchesInteractableToy(InteractableToy? interactable, Vector3 toyPosition)
    {
        if (interactable != null)
        {
            foreach (var managed in _managedInteractables)
            {
                if (ReferenceEquals(managed.Toy, interactable) ||
                    Vector3.Distance(managed.Toy.Position, interactable.Position) <= 0.05f)
                {
                    return true;
                }
            }

            // 管理Interactableを持つPrefabは、別のToyを位置半径だけで横取りしない。
            // 近接配置された複数Prefabのうち、CanInteract=falseの個体が
            // 隣のInteractableまでキャンセルすることを防ぐ。
            if (_managedInteractables.Count > 0)
                return false;
        }

        if (_managedInteractables.Any(i => Vector3.Distance(i.Toy.Position, toyPosition) <= 0.25f))
            return true;

        return ToySearchRadius > 0f && Vector3.Distance(Position, toyPosition) <= ToySearchRadius;
    }

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
    /// 派生Prefabで宣言されたpublic getter/setter付きプロパティを自動収集する。
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

        foreach (KeyValuePair<string, string> option in options)
        {
            if (option.Key.Equals(nameof(Tag), StringComparison.OrdinalIgnoreCase))
            {
                Tag = option.Value;
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

    private static bool TrySerializeOptionValue(object value, Type declaredType, out string serialized)
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

    private static bool TryDeserializeOptionValue(string serialized, Type declaredType, out object value)
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
    protected virtual void OnToySearchingNearby(PlayerSearchingToyEventArgs eventArgs) { }

    public void InvokeToySearchingNearby(PlayerSearchingToyEventArgs eventArgs)
        => OnToySearchingNearby(eventArgs);
    
    protected virtual void OnToySearchedNearby(PlayerSearchedToyEventArgs eventArgs) { }

    public void InvokeToySearchedNearby(PlayerSearchedToyEventArgs eventArgs)
        => OnToySearchedNearby(eventArgs);
    
    protected virtual void OnRoundStarted() { }
    public void InvokeRoundStarted()
        => OnRoundStarted();

    protected virtual void OnRoundRestarting()
    {
        Destroy();
    }
    public void InvokeRoundRestarting()
        => OnRoundRestarting();

    private sealed class ManagedInteractableToy
    {
        public ManagedInteractableToy(InteractableToy toy, Vector3 localOffset, Vector3 baseScale)
        {
            Toy = toy;
            LocalOffset = localOffset;
            BaseScale = baseScale;
        }

        public InteractableToy Toy { get; }
        public Vector3 LocalOffset { get; }
        public Vector3 BaseScale { get; }
    }
}

public static class InstanceManager
{
    private static readonly Dictionary<string, ObjectPrefab> _instances = new();

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
            return Enumerable.Empty<ObjectPrefab>();

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
