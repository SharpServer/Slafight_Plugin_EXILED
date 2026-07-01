using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Features;

public static class PositionProvider
{
    public static Vector3 GetNtfSpawnPosition()
    {
        return new Vector3(127f, 295.5f, -40f);
    }
    
    public static Vector3 GetChaosSpawnPosition()
    {
        return new Vector3(8f, 292f, -45f);
    }
}