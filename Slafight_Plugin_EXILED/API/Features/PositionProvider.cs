using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Features;

public static class PositionProvider
{
    public static Vector3 GetNtfSpawnPosition()
    {
        return new Vector3(127f, 295.5f, -40f);
    }
    
    public static Vector3 GetChaosSpawnPosition(float inaccuracy = 3f)
    {
        return new Vector3(8f, 292f, -45f) + new Vector3(Random.Range(-inaccuracy, inaccuracy), Random.Range(-inaccuracy, inaccuracy), Random.Range(-inaccuracy, inaccuracy));
    }

    public static Vector3 GetExitBPosition(float inaccuracy = 3f)
    {
        return new Vector3(124f, 289f, 21f) + new Vector3(Random.Range(-inaccuracy, inaccuracy), Random.Range(-inaccuracy, inaccuracy), Random.Range(-inaccuracy, inaccuracy));
    }
}