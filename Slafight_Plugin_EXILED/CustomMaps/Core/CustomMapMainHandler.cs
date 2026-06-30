using System;
using Exiled.Events.EventArgs.Player;
using LabApi.Events.CustomHandlers;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.CustomMaps.Core.DoorAccess;
using Slafight_Plugin_EXILED.CustomMaps.Core.FemurBreaker;
using Slafight_Plugin_EXILED.CustomMaps.Core.Interactions;
using Slafight_Plugin_EXILED.CustomMaps.Core.Lifecycle;
using Slafight_Plugin_EXILED.CustomMaps.Core.SurfaceGate;
using MapHandler = Exiled.Events.Handlers.Map;
using PlayerHandler = Exiled.Events.Handlers.Player;
using ServerHandler = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.CustomMaps.Core;

public class CustomMapMainHandler : CustomEventsHandler, IBootstrapHandler, IDisposable
{
    public static CustomMapMainHandler Instance { get; private set; }
    public static SpecialDoorAccessController? DoorAccess => Instance?._doorAccess;

    private const float PositionToleranceSq = 1.65f * 1.65f;
    private const float FemurJoinRadiusSq = 1.005f * 1.005f;

    public static SchematicObject Scp012_t;

    private readonly SpecialDoorAccessController _doorAccess;
    private readonly SurfaceGateBarrierController _surfaceGateBarrier;
    private readonly FemurBreakerController _femurBreaker;
    private readonly MapToyInteractionRouter _toyInteractions;
    private readonly RoundStartupCoordinator _roundStartup;
    private bool _disposed;

    public static bool _femurSetup => Instance?._femurBreaker.HasCapturedVictim ?? false;
    public static bool _femurBreaked => Instance?._femurBreaker.IsActivated ?? false;
    public static void Register()
    {
        Unregister();
        Instance = new();
        CustomHandlersManager.RegisterEventsHandler(Instance);
    }

    public static void Unregister()
    {
        if (Instance == null)
            return;

        CustomHandlersManager.UnregisterEventsHandler(Instance);
        Instance.Dispose();
        Instance = null;
    }

    public CustomMapMainHandler()
    {
        _doorAccess = new SpecialDoorAccessController(PositionToleranceSq);
        _surfaceGateBarrier = new SurfaceGateBarrierController(PositionToleranceSq);
        _femurBreaker = new FemurBreakerController(FemurJoinRadiusSq, PositionToleranceSq);
        _toyInteractions = new MapToyInteractionRouter(_surfaceGateBarrier, _femurBreaker, PositionToleranceSq);
        _roundStartup = new RoundStartupCoordinator(_doorAccess, _surfaceGateBarrier, _femurBreaker);

        MapHandler.Generated += _roundStartup.EngageServerRoomGenerators;
        ServerHandler.RoundStarted += _roundStartup.StartRound;
        ServerHandler.RestartingRound += _roundStartup.StopRound;
        MapHandler.SpawningTeamVehicle += _surfaceGateBarrier.HandleTeamVehicleSpawn;
        LabApi.Events.Handlers.PlayerEvents.SearchedToy += _toyInteractions.HandleSearchedToy;
        LabApi.Events.Handlers.ServerEvents.DoorDamaging += _doorAccess.HandleDoorDamaging;
        PlayerHandler.InteractingDoor += _doorAccess.HandleInteraction;
        PlayerHandler.Left += HandlePlayerLeft;
        PlayerHandler.Died += HandlePlayerDied;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        MapHandler.Generated -= _roundStartup.EngageServerRoomGenerators;
        ServerHandler.RoundStarted -= _roundStartup.StartRound;
        ServerHandler.RestartingRound -= _roundStartup.StopRound;
        MapHandler.SpawningTeamVehicle -= _surfaceGateBarrier.HandleTeamVehicleSpawn;
        LabApi.Events.Handlers.PlayerEvents.SearchedToy -= _toyInteractions.HandleSearchedToy;
        LabApi.Events.Handlers.ServerEvents.DoorDamaging -= _doorAccess.HandleDoorDamaging;
        PlayerHandler.InteractingDoor -= _doorAccess.HandleInteraction;
        PlayerHandler.Left -= HandlePlayerLeft;
        PlayerHandler.Died -= HandlePlayerDied;

        _roundStartup.Dispose();
        GC.SuppressFinalize(this);
    }

    private void HandlePlayerLeft(LeftEventArgs ev)
    {
        _femurBreaker.HandlePlayerLeft(ev.Player);
    }

    private void HandlePlayerDied(DiedEventArgs ev)
    {
        _femurBreaker.HandlePlayerDied(ev.Player);
    }
}
