using System;
using System.Linq;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

/// <summary>Unity object references are exported as schematic-local automatic IDs.</summary>
public sealed class UsefulDoorButton : UsefulDoorObject
{
    public string TargetIds { get; set; } = string.Empty;
    public UDoorAction Action { get; set; } = UDoorAction.Toggle;
    public UDoorButtonTargetMode TargetMode { get; set; } = UDoorButtonTargetMode.All;

    protected override void Interact(Player player) => Press();

    public UDoorInteractionResult Press()
    {
        if (Marker == null) return UDoorInteractionResult.InvalidContext;
        if (IsLocked) return UDoorInteractionResult.Locked;
        int[] ids =
        [
            .. TargetIds.Split(',').Select(value => int.TryParse(value, out int id) ? id : 0)
                .Where(id => id != 0).Distinct()
        ];
        UsefulDoor[] doors =
        [
            .. ObjectPrefabInstances.GetAllSnapshot().OfType<UsefulDoor>()
                .Where(door => door.IsInSchematic(Marker.Schematic) && ids.Contains(door.AuthoringId))
        ];
        if (doors.Length == 0) return UDoorInteractionResult.NoTarget;
        UDoorInteractionResult result = UDoorInteractionResult.NoChange;
        foreach (UsefulDoor door in doors)
        {
            UDoorInteractionResult next = door.Request(Action);
            if (next == UDoorInteractionResult.Success) result = next;
            else if (result != UDoorInteractionResult.Success) result = next;
            if (TargetMode == UDoorButtonTargetMode.First) break;
        }
        return result;
    }
}
