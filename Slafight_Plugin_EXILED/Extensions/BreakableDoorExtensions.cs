using Exiled.API.Features.Doors;
using Interactables.Interobjects.DoorUtils;
using Slafight_Plugin_EXILED.CustomMaps.Core;

namespace Slafight_Plugin_EXILED.Extensions;

public static class BreakableDoorExtensions
{
    public static bool CanBreak(this BreakableDoor breakableDoor)
    {
        if (breakableDoor == null || breakableDoor.IsDestroyed)
            return false;

        return CustomMapMainHandler.DoorAccess?.CanBreak(breakableDoor) ?? true;
    }

    public static bool ForceBreak(this BreakableDoor breakableDoor, DoorDamageType damageType = DoorDamageType.ServerCommand)
    {
        if (breakableDoor == null)
            return false;

        return CustomMapMainHandler.DoorAccess?.ForceBreak(breakableDoor, damageType) ?? breakableDoor.Break(damageType);
    }
}
