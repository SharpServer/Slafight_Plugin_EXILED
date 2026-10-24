using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;

namespace Slafight_Plugin_EXILED.Extensions;

public static class RoomUtils
{
    public static List<Room> GetSafeRooms()
    {
        List<RoomType> safeRooms = [
            RoomType.EzCrossing,
            RoomType.EzPcs,
            RoomType.Hcz127,
            RoomType.HczStraight,
            RoomType.LczClassDSpawn,
            RoomType.LczAirlock,
            RoomType.LczPlants
        ];
        return [.. Room.List.Where(r => safeRooms.Contains(r.Type) && r != null)];
    }
}