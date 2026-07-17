namespace Slafight_Plugin_EXILED.API.Enums;

/// <summary>
/// Logical UDoor set identifiers. Each enum name maps to the exact ObjectPrefab Tag
/// used by the corresponding door/button set.
/// </summary>
public enum UDoorType
{
    LczSurveillance,
}

/// <summary>Object kinds contained in a UDoor set.</summary>
public enum UDoorObjectType
{
    Door,
    Button,
}

/// <summary>Requested operation for a <see cref="CustomMaps.ObjectPrefabs.UsefulDoor"/>.</summary>
public enum UDoorAction
{
    Open,
    Close,
    Toggle,
}

/// <summary>Policy used by a door when an interaction does not provide an explicit action.</summary>
public enum UDoorActionPolicy
{
    Toggle,
    Open,
    Close,
}

/// <summary>Where a door interaction originated.</summary>
public enum UDoorInteractionSource
{
    Direct,
    Button,
    External,
}

/// <summary>Outcome of a door or button interaction.</summary>
public enum UDoorInteractionResult
{
    Success,
    Cancelled,
    Disabled,
    Locked,
    Transitioning,
    PermissionDenied,
    LimitReached,
    OneWay,
    AlreadyOpen,
    AlreadyClosed,
    NoTarget,
    NoChange,
    InvalidContext,
    Failed,
}

/// <summary>
/// Model variants contained in the central UsefulDoor/UsefulDoorButton schematics.
/// Each non-custom enum name matches an ObjectPrefabSchematicInfo key directly.
/// </summary>
public enum UDoorModelType
{
    Alpha,
    Custom,
}

/// <summary>
/// Button model variants contained in the central UsefulDoorButton schematic.
/// Each non-custom enum name matches an ObjectPrefabSchematicInfo key directly.
/// </summary>
public enum UDoorButtonModelType
{
    Standard,
    Keycard,
    Custom,
}

/// <summary>Visual child state shown inside the selected UsefulDoorButton model parent.</summary>
public enum UDoorButtonState
{
    Opening,
    Open,
    Closing,
    Close,
    Locked,
    Failed,
}

/// <summary>How a button dispatches to its target doors.</summary>
public enum UDoorButtonTargetMode
{
    All,
    First,
}
