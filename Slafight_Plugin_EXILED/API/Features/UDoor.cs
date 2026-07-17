using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using LabApi.Events.Arguments.PlayerEvents;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;
using UnityEngine;
using Player = Exiled.API.Features.Player;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// Mutable state passed through a UDoor interaction. Handlers may change
/// <see cref="RequestedAction"/>, add values to <see cref="Data"/>, or cancel
/// the operation before the target door performs its permission/state checks.
/// </summary>
public sealed class UDoorInteractionContext
{
    public UDoorInteractionContext(
        UsefulDoor door,
        Player? player,
        UDoorInteractionSource source = UDoorInteractionSource.Direct,
        UDoorAction requestedAction = UDoorAction.Toggle,
        UsefulDoorButton? button = null,
        PlayerSearchedToyEventArgs? eventArgs = null)
    {
        Door = door ?? throw new ArgumentNullException(nameof(door));
        Player = player;
        Source = source;
        RequestedAction = requestedAction;
        Button = button;
        EventArgs = eventArgs;
    }

    public UsefulDoor Door { get; }
    public UsefulDoorButton? Button { get; }
    public Player? Player { get; }
    public PlayerSearchedToyEventArgs? EventArgs { get; }
    public UDoorInteractionSource Source { get; }
    public UDoorAction RequestedAction { get; set; }
    public bool Cancelled { get; set; }
    public UDoorInteractionResult Result { get; set; } = UDoorInteractionResult.NoChange;
    public bool Succeeded => Result == UDoorInteractionResult.Success;
    public Dictionary<string, object?> Data { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void Cancel() => Cancelled = true;
}

/// <summary>
/// A snapshot view of all UDoor objects sharing an exact tag.
/// </summary>
public sealed class UDoorSet : IReadOnlyDictionary<UDoorObjectType, IReadOnlyList<ObjectPrefab>>
{
    private readonly IReadOnlyList<UsefulDoor> _doors;
    private readonly IReadOnlyList<UsefulDoorButton> _buttons;

    internal UDoorSet(string tag, IEnumerable<ObjectPrefab> prefabs)
    {
        Tag = tag?.Trim() ?? string.Empty;
        _doors = prefabs.OfType<UsefulDoor>().ToArray();
        _buttons = prefabs.OfType<UsefulDoorButton>().ToArray();
    }

    public string Tag { get; }
    public UDoorType? Type => UDoor.TryParseType(Tag, out UDoorType type) ? type : null;
    public IReadOnlyList<UsefulDoor> Door => _doors;
    public IReadOnlyList<UsefulDoorButton> Button => _buttons;
    public IReadOnlyList<UsefulDoor> Doors => _doors;
    public IReadOnlyList<UsefulDoorButton> Buttons => _buttons;

    public IReadOnlyList<ObjectPrefab> this[UDoorObjectType key]
        => key switch
        {
            UDoorObjectType.Door => _doors.Cast<ObjectPrefab>().ToArray(),
            UDoorObjectType.Button => _buttons.Cast<ObjectPrefab>().ToArray(),
            _ => Array.Empty<ObjectPrefab>(),
        };

    public IEnumerable<UDoorObjectType> Keys
    {
        get
        {
            yield return UDoorObjectType.Door;
            yield return UDoorObjectType.Button;
        }
    }

    public IEnumerable<IReadOnlyList<ObjectPrefab>> Values
    {
        get
        {
            yield return this[UDoorObjectType.Door];
            yield return this[UDoorObjectType.Button];
        }
    }

    public int Count => 2;

    public bool ContainsKey(UDoorObjectType key)
        => key == UDoorObjectType.Door || key == UDoorObjectType.Button;

    public bool TryGetValue(UDoorObjectType key, out IReadOnlyList<ObjectPrefab> value)
    {
        if (!ContainsKey(key))
        {
            value = Array.Empty<ObjectPrefab>();
            return false;
        }

        value = this[key];
        return true;
    }

    public IEnumerator<KeyValuePair<UDoorObjectType, IReadOnlyList<ObjectPrefab>>> GetEnumerator()
    {
        yield return new KeyValuePair<UDoorObjectType, IReadOnlyList<ObjectPrefab>>(UDoorObjectType.Door, this[UDoorObjectType.Door]);
        yield return new KeyValuePair<UDoorObjectType, IReadOnlyList<ObjectPrefab>>(UDoorObjectType.Button, this[UDoorObjectType.Button]);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Static UDoor facade for exact-tag grouping, interaction events, and creation helpers.
/// </summary>
public static class UDoor
{
    public static event Action<UDoorInteractionContext>? BeforeInteraction;
    public static event Action<UDoorInteractionContext>? AfterInteraction;

    public static UDoorSet GetSet(string tag)
        => new(tag, string.IsNullOrWhiteSpace(tag) ? Array.Empty<ObjectPrefab>() : InstanceManager.GetByTag(tag.Trim()).ToArray());

    public static UDoorSet GetSet(UDoorType type) => GetSet(GetTag(type));

    /// <summary>Gets the door/button set whose exact Tag is represented by <paramref name="type"/>.</summary>
    public static UDoorSet Get(UDoorType type) => GetSet(type);

    /// <summary>Returns the dictionary view for an exact tag.</summary>
    public static UDoorSet GetDictionary(string tag) => GetSet(tag);

    public static UDoorSet GetDictionary(UDoorType type) => GetSet(type);

    public static IReadOnlyList<UsefulDoor> GetDoors(string tag)
        => GetSet(tag).Door;

    public static IReadOnlyList<UsefulDoor> GetDoors(UDoorType type)
        => GetSet(type).Door;

    public static IReadOnlyList<UsefulDoorButton> GetButtons(string tag)
        => GetSet(tag).Button;

    public static IReadOnlyList<UsefulDoorButton> GetButtons(UDoorType type)
        => GetSet(type).Button;

    public static IEnumerable<TPrefab> GetByTag<TPrefab>(string tag, bool includeDerivedTypes = true)
        where TPrefab : ObjectPrefab
        => InstanceManager.GetByTag<TPrefab>(tag, includeDerivedTypes);

    public static IEnumerable<TPrefab> GetByTag<TPrefab>(UDoorType type, bool includeDerivedTypes = true)
        where TPrefab : ObjectPrefab
        => GetByTag<TPrefab>(GetTag(type), includeDerivedTypes);

    public static IReadOnlyList<TPrefab> Get<TPrefab>(string tag, bool includeDerivedTypes = true)
        where TPrefab : ObjectPrefab
        => GetByTag<TPrefab>(tag, includeDerivedTypes).ToArray();

    public static IReadOnlyList<TPrefab> Get<TPrefab>(UDoorType type, bool includeDerivedTypes = true)
        where TPrefab : ObjectPrefab
        => GetByTag<TPrefab>(type, includeDerivedTypes).ToArray();

    /// <summary>Returns the exact ObjectPrefab Tag represented by a logical door type.</summary>
    public static string GetTag(UDoorType type)
    {
        if (!Enum.IsDefined(typeof(UDoorType), type))
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown UDoor type.");

        return type.ToString();
    }

    public static bool TryParseType(string? value, out UDoorType type)
    {
        if (Enum.TryParse(value?.Trim(), true, out type) && Enum.IsDefined(typeof(UDoorType), type))
            return true;

        type = default;
        return false;
    }

    public static bool TryParseObjectType(string? value, out UDoorObjectType type)
    {
        if (Enum.TryParse(value?.Trim(), true, out type) && Enum.IsDefined(typeof(UDoorObjectType), type))
            return true;

        type = default;
        return false;
    }

    public static UDoorSet CreateSet(
        UDoorType type,
        IEnumerable<Transform>? doorTransforms,
        IEnumerable<Transform>? buttonTransforms,
        Action<UsefulDoor>? configureDoor = null,
        Action<UsefulDoorButton>? configureButton = null)
        => CreateSet(GetTag(type), doorTransforms, buttonTransforms, configureDoor, configureButton);

    public static UDoorSet CreateSet(
        string tag,
        IEnumerable<Transform>? doorTransforms,
        IEnumerable<Transform>? buttonTransforms,
        Action<UsefulDoor>? configureDoor = null,
        Action<UsefulDoorButton>? configureButton = null)
    {
        foreach (Transform transform in doorTransforms ?? Enumerable.Empty<Transform>())
        {
            if (transform == null)
                continue;

            var door = new UsefulDoor
            {
                Tag = tag,
                Position = transform.position,
                Rotation = transform.rotation,
                Scale = transform.lossyScale,
            };
            configureDoor?.Invoke(door);
            door.Create();
        }

        foreach (Transform transform in buttonTransforms ?? Enumerable.Empty<Transform>())
        {
            if (transform == null)
                continue;

            var button = new UsefulDoorButton
            {
                Tag = tag,
                Position = transform.position,
                Rotation = transform.rotation,
                Scale = transform.lossyScale,
            };
            configureButton?.Invoke(button);
            button.Create();
        }

        return GetSet(tag);
    }

    public static UDoorSet CreateSet(
        UDoorType type,
        Transform? doorTransform,
        Transform? buttonTransform,
        Action<UsefulDoor>? configureDoor = null,
        Action<UsefulDoorButton>? configureButton = null)
        => CreateSet(GetTag(type), doorTransform, buttonTransform, configureDoor, configureButton);

    public static UDoorSet CreateSet(
        string tag,
        Transform? doorTransform,
        Transform? buttonTransform,
        Action<UsefulDoor>? configureDoor = null,
        Action<UsefulDoorButton>? configureButton = null)
        => CreateSet(
            tag,
            doorTransform == null ? null : new[] { doorTransform },
            buttonTransform == null ? null : new[] { buttonTransform },
            configureDoor,
            configureButton);

    internal static bool RaiseBefore(UDoorInteractionContext context)
    {
        try
        {
            BeforeInteraction?.Invoke(context);
        }
        catch (Exception e)
        {
            Log.Warn($"[UDoor] BeforeInteraction handler failed: {e.Message}");
        }

        return !context.Cancelled;
    }

    internal static void RaiseAfter(UDoorInteractionContext context)
    {
        try
        {
            AfterInteraction?.Invoke(context);
        }
        catch (Exception e)
        {
            Log.Warn($"[UDoor] AfterInteraction handler failed: {e.Message}");
        }
    }

    internal static void NotifyLinkedButtons(UsefulDoor door, UDoorButtonState state)
    {
        if (door == null || string.IsNullOrWhiteSpace(door.Tag))
            return;

        foreach (UsefulDoorButton button in InstanceManager.GetAll().OfType<UsefulDoorButton>())
        {
            if (button.TargetsTag(door.Tag))
                button.SetDoorState(state);
        }
    }
}
