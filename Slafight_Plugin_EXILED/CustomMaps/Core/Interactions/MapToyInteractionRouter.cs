using LabApi.Events.Arguments.PlayerEvents;
using Slafight_Plugin_EXILED.CustomMaps.Core.FemurBreaker;
using Slafight_Plugin_EXILED.CustomMaps.Core.SurfaceGate;
using Slafight_Plugin_EXILED.CustomMaps.Features;
using Slafight_Plugin_EXILED.SpecialEvents;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps.Core.Interactions;

internal sealed class MapToyInteractionRouter
{
    private readonly SurfaceGateBarrierController _surfaceGateBarrier;
    private readonly FemurBreakerController _femurBreaker;
    private readonly float _positionToleranceSq;

    public MapToyInteractionRouter(
        SurfaceGateBarrierController surfaceGateBarrier,
        FemurBreakerController femurBreaker,
        float positionToleranceSq)
    {
        _surfaceGateBarrier = surfaceGateBarrier;
        _femurBreaker = femurBreaker;
        _positionToleranceSq = positionToleranceSq;
    }

    public void HandleSearchedToy(PlayerSearchedToyEventArgs ev)
    {
        if (ev.Interactable == null)
            return;

        var position = ev.Interactable.Position;

        _surfaceGateBarrier.HandleManualButton(position);
        _femurBreaker.HandleButton(position, ev.Player);

        if (IsOmegaWarheadButton(position))
            StartOmegaWarhead(ev.Player);
    }

    private bool IsOmegaWarheadButton(Vector3 position)
    {
        return MapFlags.OmegaWarheadButton != default &&
               Vector3.SqrMagnitude(position - MapFlags.OmegaWarheadButton) <= _positionToleranceSq;
    }

    private static void StartOmegaWarhead(LabApi.Features.Wrappers.Player labPlayer)
    {
        if (!SpecialEventsHandler.IsWarheadable() || OmegaWarhead.IsWarheadStarted)
        {
            labPlayer.SendHint("何らかの要因で実行できませんでした");
            return;
        }

        var player = Exiled.API.Features.Player.Get(labPlayer.NetworkId);
        OmegaWarhead.StartProtocol(0f, startedBy: player);
    }
}
