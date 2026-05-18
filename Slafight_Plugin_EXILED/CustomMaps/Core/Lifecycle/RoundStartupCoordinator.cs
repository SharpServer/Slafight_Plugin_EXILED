using System;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using MEC;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.Changes;
using Slafight_Plugin_EXILED.Commands.DevTools;
using Slafight_Plugin_EXILED.CustomMaps.Core.DoorAccess;
using Slafight_Plugin_EXILED.CustomMaps.Core.FemurBreaker;
using Slafight_Plugin_EXILED.CustomMaps.Core.SurfaceGate;
using Slafight_Plugin_EXILED.CustomMaps.Features;
using Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

namespace Slafight_Plugin_EXILED.CustomMaps.Core.Lifecycle;

internal sealed class RoundStartupCoordinator : IDisposable
{
    private readonly SpecialDoorAccessController _doorAccess;
    private readonly SurfaceGateBarrierController _surfaceGateBarrier;
    private readonly FemurBreakerController _femurBreaker;
    private CoroutineHandle _trainCoroutine;

    public RoundStartupCoordinator(
        SpecialDoorAccessController doorAccess,
        SurfaceGateBarrierController surfaceGateBarrier,
        FemurBreakerController femurBreaker)
    {
        _doorAccess = doorAccess;
        _surfaceGateBarrier = surfaceGateBarrier;
        _femurBreaker = femurBreaker;
    }

    public void EngageServerRoomGenerators()
    {
        foreach (var generator in Generator.List.Where(g => g.Room.Type == RoomType.HczServerRoom))
            generator.IsEngaged = true;
    }

    public void StartRound()
    {
        StopWarheadEffects();
        StopCoroutines();

        LoadBaseMapAndFeatures();
        LoadSeasonMap();
        SetCandyState();

        Timing.CallDelayed(2.3f, () =>
        {
            _doorAccess.ConfigureForCurrentMap();
            _doorAccess.ApplyDoorState();
        });
    }

    public static void StopWarheadEffects()
    {
        WarheadBoomEffectUtil.StopAllEffects();
    }

    public void Dispose()
    {
        StopCoroutines();
        _doorAccess.Clear();
        _surfaceGateBarrier.Clear();
        _femurBreaker.Clear();
    }

    private void StopCoroutines()
    {
        _femurBreaker.StopMonitoring();

        if (_trainCoroutine.IsRunning)
            Timing.KillCoroutines(_trainCoroutine);
    }

    private static void SetCandyState()
    {
        Timing.CallDelayed(3f, () =>
        {
            if (!CandyChanges.CandyChances.ContainsKey("Default"))
                CandyChanges.Init();

            if (MapFlags.GetSeason() == SeasonTypeId.April)
            {
                CandyChanges.CandyChances.TryGetValue("Default", out var result);
                result.MostRareChance = 0.22f;
                result.RareCandiesChance = 0.5f;
                CandyChanges.TryAddDictionary("April", result);
                CandyChanges.TrySetActiveDictionary("April", out _);
                return;
            }

            CandyChanges.TrySetActiveDictionary("Default", out _);
        });
    }

    private void LoadBaseMapAndFeatures()
    {
        WarheadBoomEffectUtil.StopAllEffects();
        OmegaWarhead.Reset();
        ObjectPrefabLoader.LoadMap("aaa");

        Timing.CallDelayed(2.25f, () =>
        {
            BindLoadedSchematics();
            _femurBreaker.StartMonitoringIfReady();
            StartTrainIfReady();
            SpawnAntiAntiMemeDocument();
        });
    }

    private void BindLoadedSchematics()
    {
        _surfaceGateBarrier.Clear();
        _femurBreaker.Clear();
        CustomMapMainHandler.Scp012_t = null;

        foreach (var map in MapUtils.LoadedMaps.Values)
        {
            if (map.SpawnedObjects == null)
                continue;

            foreach (var mapEditorObject in map.SpawnedObjects)
            {
                if (mapEditorObject.TryGetComponent(out SchematicObject schematic))
                    BindSchematic(schematic);
            }
        }
    }

    private void BindSchematic(SchematicObject schematic)
    {
        switch (schematic.Name)
        {
            case "Surface_CarStopper_Bar":
                _surfaceGateBarrier.SetBarrier(schematic);
                break;
            case "FemurBreaker_Door":
                _femurBreaker.SetDoor(schematic);
                break;
            case "FemurBreakerButton":
                _femurBreaker.SetButton(schematic);
                break;
            case "Scp012_ThetaPrimed":
                CustomMapMainHandler.Scp012_t = schematic;
                break;
        }
    }

    private void StartTrainIfReady()
    {
        if (MapFlags.TrainStartPoint == default ||
            MapFlags.TrainCheckpointPoint == default ||
            MapFlags.TrainEndPoint == default)
        {
            Log.Error("Train Points not successfully spawned.");
            return;
        }

        Timing.CallDelayed(25f, () =>
        {
            if (!Round.InProgress)
                return;

            _trainCoroutine = Timing.RunCoroutine(TrainComing.SpawnTrainAndAnim(
                MapFlags.TrainStartPoint,
                MapFlags.TrainCheckpointPoint,
                MapFlags.TrainEndPoint));
        });
    }

    private static void SpawnAntiAntiMemeDocument()
    {
        if (MapFlags.AntiAntiMemeDocPoint == default)
            return;

        var doc = new Document().Create() as Document;
        if (doc == null)
            return;

        doc.DocumentType = DocumentType.AntiAntiMeme;
        doc.Position = MapFlags.AntiAntiMemeDocPoint;
        doc.ShowModel = false;
    }

    private static void LoadSeasonMap()
    {
        switch (MapFlags.GetSeason())
        {
            case SeasonTypeId.Halloween:
                MapUtils.LoadMap("Holiday_HalloweenMap");
                break;
            case SeasonTypeId.Christmas:
                MapUtils.LoadMap("Holiday_ChristmasMap");
                break;
        }
    }
}
