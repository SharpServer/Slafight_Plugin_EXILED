using Exiled.API.Enums;
using Exiled.API.Features.Doors;

namespace Slafight_Plugin_EXILED.CustomMaps.Features;

public static class WarheadDoorLockdown
{
    public static void LockAllDoorsClosed(DoorLockType lockType = DoorLockType.Warhead)
    {
        foreach (var door in Door.List)
        {
            if (door == null)
                continue;

            if (door.IsElevator)
            {
                door.Lock(lockType);
                continue;
            }

            door.IsOpen = false;
            door.Lock(lockType);
        }
    }
}
