using Exiled.API.Enums;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.Handlers;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.Changes;

public class ScpPermissions : IBootstrapHandler
{
    public static void Register()
    {
        Player.InteractingDoor += OnKeyDoor;
    }

    public static void Unregister()
    {
        Player.InteractingDoor -= OnKeyDoor;
    }

    private static void OnKeyDoor(InteractingDoorEventArgs ev)
    {
        if (ev.Player is null || ev.Player.GetTeam() is not CTeam.SCPs) return;
        if (ev.Door.Type is DoorType.Checkpoint or DoorType.CheckpointArmoryA or DoorType.CheckpointArmoryB or DoorType.CheckpointEzHczA or DoorType.CheckpointEzHczB or DoorType.CheckpointLczA or DoorType.CheckpointLczB or DoorType.GateAArmory)
        {
            ev.IsAllowed = true;
        }
    }
}