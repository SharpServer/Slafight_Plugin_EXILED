using System;
using Exiled.API.Features;
using LabApi.Events.Handlers;
using MEC;
using ProjectMER.Events.Arguments;
using ProjectMER.Events.Handlers;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;
using Logger = LabApi.Features.Console.Logger;

namespace Slafight_Plugin_EXILED.CustomMaps.Bridges;

public class TriggerPointRegistry : SlafightLabApiHandler
{
    private static int _refreshGeneration;
    private static CoroutineHandle _refreshHandle;

    protected override void RegisterEvents(EventSubscriptionScope subscriptions)
    {
        subscriptions.Add(
            () => ServerEvents.WaitingForPlayers += OnWaitingForPlayers,
            () => ServerEvents.WaitingForPlayers -= OnWaitingForPlayers);
        subscriptions.Add(
            () => Schematic.SchematicSpawned += OnSchematicSpawned,
            () => Schematic.SchematicSpawned -= OnSchematicSpawned);
    }

    protected override void OnDisposed()
    {
        _refreshGeneration++;
        Timing.KillCoroutines(_refreshHandle);
    }

    private static void OnWaitingForPlayers()
    {
        Logger.Info("LabApi Loader: Green");
        ScheduleRefresh(5.0f);
    }

    private static void OnSchematicSpawned(SchematicSpawnedEventArgs ev)
    {
        ScheduleRefresh(0.1f);
    }

    private static void ScheduleRefresh(float delay)
    {
        int generation = ++_refreshGeneration;
        Timing.KillCoroutines(_refreshHandle);
        _refreshHandle = Timing.CallDelayed(delay, () =>
        {
            if (generation != _refreshGeneration)
                return;

            RefreshTriggerPoints();
        });
    }

    private static void RefreshTriggerPoints()
    {
        MapFlags.ResetTriggerPoints();
        RegisterTriggerPoints();
    }

    private static void RegisterTriggerPoints()
    {
        foreach (var point in CustomTriggerPoint.GetAll())
        {
            try
            {
                RegisterByTag(point);
            }
            catch (Exception e)
            {
                Log.Error($"[TriggerPointRegistry] Error while registering trigger point {point.Tag}: {e}");
            }
        }
    }

    private static void RegisterByTag(CustomTriggerPoint point)
    {
        var tag = point.Tag;
        var pos = point.Position;

        switch (tag)
        {
            case "Scp173SpawnPoint":
                MapFlags.Scp173SpawnPoint = pos + Vector3.up * 0.05f;
                break;
            case "Scp682SpawnPoint":
                MapFlags.Scp682SpawnPoint = pos;
                break;
            case "Scp3005SpawnPoint":
                MapFlags.Scp3005SpawnPoint = pos;
                break;
            case "FacilityManagerSpawnPoint":
                MapFlags.FacilityManagerSpawnPoint = pos;
                break;
            case "FirstTeamSpawnPoint":
                MapFlags.FirstTeamSpawnPoint = pos;
                break;
            case "SupplyManagerSpawnPointA":
                MapFlags.SupplyManagerSpawnPointA = pos;
                break;
            case "SupplyManagerSpawnPointB":
                MapFlags.SupplyManagerSpawnPointB = pos;
                break;
            case "FemurBreaker_JoinPoint":
                MapFlags.FemurBreakerJoinPoint = pos;
                break;
            case "FemurBreaker_CapybaraPoint":
                MapFlags.FemurBreakerCapybaraPoint = pos;
                break;
            case "PDEX_JoinPoint":
                MapFlags.PocketDimensionExitJoinPoint = pos;
                break;
            case "PDEX_JoinPointKing":
                MapFlags.PocketDimensionExitKingJoinPoint = pos;
                break;
            case "OWB":
                MapFlags.OmegaWarheadButton = pos;
                break;
            case "OWJoin":
                MapFlags.OmegaWarheadJoinPoint = pos;
                break;
            case "ST_S":
                MapFlags.TrainStartPoint = pos;
                break;
            case "ST_C":
                MapFlags.TrainCheckpointPoint = pos;
                break;
            case "ST_E":
                MapFlags.TrainEndPoint = pos;
                break;
            case "AntiAntiMemeDoc":
                MapFlags.AntiAntiMemeDocPoint = pos;
                break;
            case "SQ_Door":
                MapFlags.SqDoorPoint = pos;
                break;
            case "CDoor_O1":
                MapFlags.CDoorO1 = pos;
                break;
            case "CDoor_O2":
                MapFlags.CDoorO2 = pos;
                break;
            case "CDoor_O3":
                MapFlags.CDoorO3 = pos;
                break;
            case "CDoor_O4":
                MapFlags.CDoorO4 = pos;
                break;
            case "AntiMemeButton":
                MapFlags.AntiMemeButton = pos;
                break;
            case "EscapePoint":
                MapFlags.EscapePoints.Add(pos);
                break;
            case "EZCInteractable":
                MapFlags.EzcInteractablePoint = pos;
                break;
            case "EZCScreen":
                MapFlags.EzcScreenPoint = pos;
                MapFlags.EzcScreenRotation = point.Rotation;
                break;
            case "EzPcTentaclePoint":
                MapFlags.EzPcTentaclePoint = pos;
                break;
            case "HczSQ":
                MapFlags.HczOverbeyondDocumentPoint = pos;
                break;
            case "HczASQ":
                MapFlags.HczAboutSqDocumentPoint = pos;
                break;
            case "LczS3005D":
                MapFlags.LczScp3005DocumentPoint = pos;
                break;
            case "BroadcasterPoint":
                MapFlags.BroadcasterPoint = pos;
                break;
        }
    }
}
