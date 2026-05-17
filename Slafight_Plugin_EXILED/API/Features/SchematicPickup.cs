#nullable enable
using System;
using System.Collections.Generic;
using AdminToys;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using MEC;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using UnityEngine;
using MapHandlers = Exiled.Events.Handlers.Map;
using MapEvents = Exiled.Events.EventArgs.Map;
using Player = Exiled.API.Features.Player;
using InteractableToy = LabApi.Features.Wrappers.InteractableToy;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// Schematic だけで構成する、Pickup 風の汎用オブジェクト。
/// 実 Exiled Pickup / PickupBase ではないため Pickup への cast はできない。
/// </summary>
public sealed class SchematicPickup
{
    private static readonly List<SchematicPickup> ActivePickups = [];
    private static readonly Dictionary<ushort, SchematicPickup> BackingPickupIndex = [];
    private static bool _eventsSubscribed;

    private bool _destroyed;
    private CoroutineHandle _backingPickupTracker;

    private SchematicPickup(
        SchematicPickupSettings settings,
        SchematicObject schematic,
        InteractableToy interactable,
        IReadOnlyList<Rigidbody> rigidbodies,
        Pickup? backingPickup)
    {
        Settings = settings;
        Schematic = schematic;
        Interactable = interactable;
        Rigidbodies = rigidbodies;
        BackingPickup = backingPickup;
    }

    public event Action<SchematicPickup, Player>? Searched;
    public event Action<SchematicPickup>? Destroyed;

    public SchematicPickupSettings Settings { get; }
    public SchematicObject Schematic { get; }
    public InteractableToy Interactable { get; }
    public Pickup? BackingPickup { get; }
    public IReadOnlyList<Rigidbody> Rigidbodies { get; }

    public Vector3 Position => Schematic?.Position ?? Interactable.Position;
    public Quaternion Rotation => Schematic?.Rotation ?? Interactable.Rotation;
    public bool IsDestroyed => _destroyed;
    public bool HasBackingPickup => BackingPickup?.Base != null;

    public Pickup? AsPickup()
        => HasBackingPickup ? BackingPickup : null;

    public bool TryGetPickup(out Pickup? pickup)
    {
        pickup = AsPickup();
        return pickup != null;
    }

    public static IReadOnlyList<SchematicPickup> Active => ActivePickups;

    public static SchematicPickupSettings ForVanillaItem(string schematicName, ItemType itemType, Vector3 position)
        => new()
        {
            SchematicName = schematicName,
            Position = position,
            BackingPickupItemType = itemType,
            OnSearched = (_, player) => player.AddItem(itemType) != null,
        };

    public static SchematicPickupSettings ForCItem(string schematicName, CItem cItem, Vector3 position, bool displayMessage = true)
        => new()
        {
            SchematicName = schematicName,
            Position = position,
            BackingPickupItemType = cItem.GetBaseItem(),
            OnSearched = (_, player) => cItem.Give(player, displayMessage) != null,
        };

    public static SchematicPickup? Spawn(SchematicPickupSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        if (string.IsNullOrWhiteSpace(settings.SchematicName))
        {
            Log.Warn("SchematicPickup.Spawn: SchematicName is empty.");
            return null;
        }

        try
        {
            var schematicRotation = settings.Rotation * Quaternion.Euler(settings.SchematicRotationOffset);
            var schematic = ObjectSpawner.SpawnSchematic(
                settings.SchematicName,
                settings.Position + settings.Rotation * settings.SchematicOffset,
                schematicRotation,
                settings.SchematicScale);

            if (schematic == null)
            {
                Log.Warn($"SchematicPickup.Spawn: failed to spawn schematic '{settings.SchematicName}'.");
                return null;
            }

            var rigidbodies = ConfigurePhysics(schematic.gameObject, settings);
            var backingPickup = CreateBackingPickup(settings);

            var interactable = InteractableToy.Create();
            interactable.Position = schematic.Position + schematic.Rotation * settings.InteractableOffset;
            interactable.Rotation = schematic.Rotation * Quaternion.Euler(settings.InteractableRotationOffset);
            interactable.Scale = settings.InteractableScale;
            interactable.Shape = settings.InteractableShape;
            interactable.InteractionDuration = settings.InteractionDuration;
            interactable.Spawn();
            interactable.Base.transform.SetParent(schematic.transform, true);

            var pickup = new SchematicPickup(settings, schematic, interactable, rigidbodies, backingPickup);
            interactable.Base.OnSearched += pickup.HandleSearched;

            if (backingPickup != null)
            {
                EnsureEventSubscription();
                BackingPickupIndex[backingPickup.Serial] = pickup;
                pickup._backingPickupTracker = Timing.RunCoroutine(pickup.TrackBackingPickup());
            }

            ActivePickups.Add(pickup);
            return pickup;
        }
        catch (Exception ex)
        {
            Log.Error($"SchematicPickup.Spawn failed: {ex}");
            return null;
        }
    }

    public static void DestroyAll()
    {
        foreach (var pickup in ActivePickups.ToArray())
            pickup.Destroy();

        ActivePickups.Clear();
    }

    public void Destroy(float delay = 0f)
    {
        if (_destroyed) return;

        if (delay > 0f)
        {
            Timing.CallDelayed(delay, () => Destroy());
            return;
        }

        _destroyed = true;

        try { Interactable.Base.OnSearched -= HandleSearched; }
        catch (Exception ex) { Log.Warn($"SchematicPickup unsubscribe failed: {ex}"); }

        try { Interactable.Destroy(); }
        catch (Exception ex) { Log.Warn($"SchematicPickup interactable destroy failed: {ex}"); }

        try { Schematic.Destroy(); }
        catch (Exception ex) { Log.Warn($"SchematicPickup schematic destroy failed: {ex}"); }

        if (_backingPickupTracker.IsRunning)
            Timing.KillCoroutines(_backingPickupTracker);

        if (BackingPickup != null)
        {
            BackingPickupIndex.Remove(BackingPickup.Serial);

            try
            {
                if (BackingPickup.Base != null)
                    BackingPickup.Destroy();
            }
            catch (Exception ex) { Log.Warn($"SchematicPickup backing pickup destroy failed: {ex}"); }
        }

        ActivePickups.Remove(this);
        Destroyed?.Invoke(this);
    }

    private IEnumerator<float> TrackBackingPickup()
    {
        while (!_destroyed && BackingPickup?.Base != null && Schematic?.gameObject != null)
        {
            BackingPickup.Position = Schematic.Position;
            BackingPickup.Rotation = Schematic.Rotation;
            yield return Timing.WaitForOneFrame;
        }
    }

    private void HandleSearched(ReferenceHub hub)
    {
        if (_destroyed || hub == null) return;

        var player = Player.Get(hub);
        if (player == null) return;

        if (Settings.RequireAlivePlayer && !player.IsAlive)
            return;

        if (Settings.RequireInventorySpace && player.IsInventoryFull)
        {
            if (!string.IsNullOrEmpty(Settings.InventoryFullHint))
                player.ShowHint(Settings.InventoryFullHint, Settings.InventoryFullHintDuration);
            return;
        }

        var handled = false;
        if (Settings.OnSearched != null)
        {
            try { handled = Settings.OnSearched(this, player); }
            catch (Exception ex) { Log.Error($"SchematicPickup.OnSearched failed: {ex}"); }
        }

        Searched?.Invoke(this, player);

        if (handled && Settings.DestroyAfterSuccessfulSearch)
            Destroy();
    }

    private static IReadOnlyList<Rigidbody> ConfigurePhysics(GameObject? root, SchematicPickupSettings settings)
    {
        if (root == null) return [];

        var rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
        if (rigidbodies.Length == 0)
            rigidbodies = [root.GetComponent<Rigidbody>() ?? root.AddComponent<Rigidbody>()];

        foreach (var rb in rigidbodies)
        {
            if (rb == null) continue;
            rb.isKinematic = settings.IsKinematic;
            rb.useGravity = settings.UseGravity;
            rb.mass = settings.RigidbodyMass;
            rb.drag = settings.RigidbodyDrag;
            rb.angularDrag = settings.RigidbodyAngularDrag;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        return rigidbodies;
    }

    private static Pickup? CreateBackingPickup(SchematicPickupSettings settings)
    {
        if (settings.BackingPickupItemType == null)
            return null;

        var pickup = Pickup.CreateAndSpawn(
            settings.BackingPickupItemType.Value,
            settings.Position,
            settings.Rotation);

        if (pickup == null)
            return null;

        pickup.Scale = settings.HideBackingPickup
            ? settings.HiddenBackingPickupScale
            : settings.BackingPickupScale;

        return pickup;
    }

    private static void EnsureEventSubscription()
    {
        if (_eventsSubscribed) return;

        MapHandlers.PickupDestroyed += OnAnyPickupDestroyed;
        _eventsSubscribed = true;
    }

    private static void OnAnyPickupDestroyed(MapEvents.PickupDestroyedEventArgs ev)
    {
        if (ev?.Pickup == null) return;
        if (!BackingPickupIndex.TryGetValue(ev.Pickup.Serial, out var schematicPickup)) return;

        schematicPickup.Destroy();
    }
}

public sealed class SchematicPickupSettings
{
    public string SchematicName { get; set; } = string.Empty;

    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; } = Quaternion.identity;

    public Vector3 SchematicOffset { get; set; } = Vector3.zero;
    public Vector3 SchematicRotationOffset { get; set; } = Vector3.zero;
    public Vector3 SchematicScale { get; set; } = Vector3.one;

    public Vector3 InteractableOffset { get; set; } = Vector3.zero;
    public Vector3 InteractableRotationOffset { get; set; } = Vector3.zero;
    public Vector3 InteractableScale { get; set; } = Vector3.one;
    public InvisibleInteractableToy.ColliderShape InteractableShape { get; set; } =
        InvisibleInteractableToy.ColliderShape.Box;
    public float InteractionDuration { get; set; } = 0.25f;

    public bool UseGravity { get; set; } = true;
    public bool IsKinematic { get; set; }
    public float RigidbodyMass { get; set; } = 1f;
    public float RigidbodyDrag { get; set; }
    public float RigidbodyAngularDrag { get; set; } = 0.05f;

    public ItemType? BackingPickupItemType { get; set; }
    public bool HideBackingPickup { get; set; } = true;
    public Vector3 HiddenBackingPickupScale { get; set; } = Vector3.zero;
    public Vector3 BackingPickupScale { get; set; } = Vector3.one;

    public bool RequireAlivePlayer { get; set; } = true;
    public bool RequireInventorySpace { get; set; } = true;
    public bool DestroyAfterSuccessfulSearch { get; set; } = true;
    public string InventoryFullHint { get; set; } = "<size=24>インベントリがいっぱいです。</size>";
    public float InventoryFullHintDuration { get; set; } = 2f;

    public Func<SchematicPickup, Player, bool>? OnSearched { get; set; }
}
