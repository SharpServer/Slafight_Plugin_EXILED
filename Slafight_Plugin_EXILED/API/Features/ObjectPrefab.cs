using System;
using System.Collections.Generic;
using System.Linq;
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

public abstract class ObjectPrefab : IObjectPrefab
{
    private readonly List<ManagedInteractableToy> _managedInteractables = [];
    private readonly List<CoroutineHandle> _scheduledCallbacks = [];
    private Vector3 _position = Vector3.zero;
    private Quaternion _rotation = Quaternion.identity;
    private Vector3 _scale = Vector3.one;
    private string _objectInstanceID = string.Empty;
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
    public virtual bool IsSaveable { get; set; } = true;

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
        }

        if (_managedInteractables.Any(i => Vector3.Distance(i.Toy.Position, toyPosition) <= 0.25f))
            return true;

        return ToySearchRadius > 0f && Vector3.Distance(Position, toyPosition) <= ToySearchRadius;
    }

    /// <summary>
    /// サブクラスが独自オプションを保存するときにoverrideする。
    /// </summary>
    public virtual Dictionary<string, string> CollectOptions() => new();

    /// <summary>
    /// サブクラスがロード時に独自オプションを復元するときにoverrideする。
    /// </summary>
    public virtual void ApplyOptions(Dictionary<string, string> options) { }

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