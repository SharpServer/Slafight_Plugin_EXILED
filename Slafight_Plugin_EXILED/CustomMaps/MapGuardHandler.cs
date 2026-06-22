using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Warhead;
using Interactables.Interobjects.DoorUtils;
using MapGeneration;
using MEC;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps;

public static class MapGuardHandler
{
    public static void Register()
    {
        Exiled.Events.Handlers.Player.DamagingDoor += OnExplodingDoor;
        Exiled.Events.Handlers.Warhead.Starting += OnWarheadDoor;
    }

    public static void Unregister()
    {
        Exiled.Events.Handlers.Player.DamagingDoor -= OnExplodingDoor;
        Exiled.Events.Handlers.Warhead.Starting -= OnWarheadDoor;
    }

    private static void OnExplodingDoor(DamagingDoorEventArgs ev)
    {
        if (ev.DamageType != DoorDamageType.ServerCommand)
        {
            if (ev.Door == null) return;
            
            if (!(Vector3.SqrMagnitude(ev.Door.Position - MapFlags.OmegaWarheadJoinPoint) > 0.75 * 0.75))
            {
                ev.IsAllowed = false;
                Message(ev.Player);
            }

            if (ev.Door.Type == DoorType.LightContainmentDoor)
            {
                var _ = ev.Door.Position.TryGetRoom(out var room);
                if (room.Name != RoomName.EzGateB) return;
                ev.IsAllowed = false;
                Message(ev.Player);
            }
        }
    }

    private static void OnWarheadDoor(StartingEventArgs ev)
    {
        Timing.CallDelayed(30f, () =>
        {
            foreach (var door in Door.List)
            {
                if (door == null) continue;
                if (!(Vector3.SqrMagnitude(door.Position - MapFlags.OmegaWarheadJoinPoint) > 0.75 * 0.75))
                {
                    door.Unlock();
                    door.IsOpen = false;
                    door.Lock(DoorLockType.AdminCommand);
                    continue;
                }

                if (door.Type == DoorType.LightContainmentDoor)
                {
                    var _ = door.Position.TryGetRoom(out var room);
                    if (room.Name != RoomName.EzGateB) continue;
                    door.Unlock();
                    door.IsOpen = false;
                    door.Lock(DoorLockType.AdminCommand);
                }

                if (!(Vector3.SqrMagnitude(door.Position - new Vector3(139.153f, 300.128f, -65.381f)) > 0.75 * 0.75))
                {
                    door.Unlock();
                    door.IsOpen = false;
                    door.Lock(DoorLockType.AdminCommand);
                    continue;
                }
            }
        });
    }

    private static void Message(Player? player)
    {
        player.ShowHint("この扉は特殊な素材で守られているようだ・・・");
    }
}

