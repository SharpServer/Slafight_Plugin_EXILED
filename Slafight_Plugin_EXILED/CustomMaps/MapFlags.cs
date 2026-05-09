using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.CustomMaps.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps;

public static class MapFlags
{
    public static bool FemurSetup => CustomMapMainHandler._femurSetup;
    public static bool FemurBreaked => CustomMapMainHandler._femurBreaked;
    public static bool IsOmegaStarted => OmegaWarhead.IsWarheadStarted;
    public static bool IsWarheadBooming => WarheadBoomEffectHandler.IsBooming;
    public static bool IsOverrideActivated = false;
    public static Vector3 Scp682SpawnPoint = Vector3.zero;
    public static Vector3 Scp3005SpawnPoint = Vector3.zero;
    public static Vector3 FacilityManagerSpawnPoint = Vector3.zero;
    public static Vector3 AntiAntiMemeDocPoint = Vector3.zero;
    public static Vector3 SqDoorPoint = Vector3.zero;
    
    public static Vector3 FirstTeamSpawnPoint = Vector3.zero;

    /// <summary>
    /// Get Season. Please look to 
    /// <see cref="Config.Season"/>
    /// </summary>
    public static SeasonTypeId GetSeason()
    {
        return Plugin.Singleton.Config.Season switch
        {
            0 => SeasonTypeId.None,
            1 => SeasonTypeId.Halloween,
            2 => SeasonTypeId.Christmas,
            3 => SeasonTypeId.April,
            4 => SeasonTypeId.FifthFestival,
            _ => SeasonTypeId.None
        };
    }
}