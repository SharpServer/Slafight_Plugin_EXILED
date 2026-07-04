using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.CustomMaps.Core;
using Slafight_Plugin_EXILED.CustomMaps.Features;
using System.Collections.Generic;
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
    public static Vector3 Scp173SpawnPoint = Vector3.zero;
    public static Vector3 Scp3005SpawnPoint = Vector3.zero;
    public static Vector3 FacilityManagerSpawnPoint = Vector3.zero;
    public static Vector3 AntiAntiMemeDocPoint = Vector3.zero;
    public static Vector3 SqDoorPoint = Vector3.zero;
    public static Vector3 AntiMemeButton = Vector3.zero;
    public static Vector3 SupplyManagerSpawnPointA = Vector3.zero;
    public static Vector3 SupplyManagerSpawnPointB = Vector3.zero;
    public static Vector3 FemurBreakerJoinPoint = Vector3.zero;
    public static Vector3 FemurBreakerCapybaraPoint = Vector3.zero;
    public static Vector3 PocketDimensionExitJoinPoint = Vector3.zero;
    public static Vector3 PocketDimensionExitKingJoinPoint = Vector3.zero;
    public static Vector3 OmegaWarheadButton = Vector3.zero;
    public static Vector3 OmegaWarheadJoinPoint = Vector3.zero;
    public static Vector3 TrainStartPoint = Vector3.zero;
    public static Vector3 TrainCheckpointPoint = Vector3.zero;
    public static Vector3 TrainEndPoint = Vector3.zero;
    public static Vector3 EzcInteractablePoint = Vector3.zero;
    public static Vector3 EzcScreenPoint = Vector3.zero;
    public static Quaternion EzcScreenRotation = Quaternion.identity;
    public static Vector3 EzPcTentaclePoint = Vector3.zero;
    public static Vector3 HczOverbeyondDocumentPoint = Vector3.zero;
    public static Vector3 HczAboutSqDocumentPoint = Vector3.zero;
    public static Vector3 LczScp3005DocumentPoint = Vector3.zero;
    public static Vector3 CDoorO1 = Vector3.zero;
    public static Vector3 CDoorO2 = Vector3.zero;
    public static Vector3 CDoorO3 = Vector3.zero;
    public static Vector3 CDoorO4 = Vector3.zero;
    public static Vector3 BroadcasterPoint = Vector3.zero;
    public static readonly List<Vector3> EscapePoints = [];
    
    public static Vector3 FirstTeamSpawnPoint = Vector3.zero;

    public static void ResetTriggerPoints()
    {
        Scp682SpawnPoint = Vector3.zero;
        Scp173SpawnPoint = Vector3.zero;
        Scp3005SpawnPoint = Vector3.zero;
        FacilityManagerSpawnPoint = Vector3.zero;
        AntiAntiMemeDocPoint = Vector3.zero;
        SqDoorPoint = Vector3.zero;
        AntiMemeButton = Vector3.zero;
        SupplyManagerSpawnPointA = Vector3.zero;
        SupplyManagerSpawnPointB = Vector3.zero;
        FemurBreakerJoinPoint = Vector3.zero;
        FemurBreakerCapybaraPoint = Vector3.zero;
        PocketDimensionExitJoinPoint = Vector3.zero;
        PocketDimensionExitKingJoinPoint = Vector3.zero;
        OmegaWarheadButton = Vector3.zero;
        OmegaWarheadJoinPoint = Vector3.zero;
        TrainStartPoint = Vector3.zero;
        TrainCheckpointPoint = Vector3.zero;
        TrainEndPoint = Vector3.zero;
        EzcInteractablePoint = Vector3.zero;
        EzcScreenPoint = Vector3.zero;
        EzcScreenRotation = Quaternion.identity;
        EzPcTentaclePoint = Vector3.zero;
        HczOverbeyondDocumentPoint = Vector3.zero;
        HczAboutSqDocumentPoint = Vector3.zero;
        LczScp3005DocumentPoint = Vector3.zero;
        FirstTeamSpawnPoint = Vector3.zero;
        EscapePoints.Clear();
    }

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
            5 => SeasonTypeId.Summer,
            _ => SeasonTypeId.None
        };
    }
}
