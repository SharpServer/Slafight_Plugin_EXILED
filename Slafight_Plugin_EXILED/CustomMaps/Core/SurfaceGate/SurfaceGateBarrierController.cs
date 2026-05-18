using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Map;
using MEC;
using PlayerRoles;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.CustomMaps.Core.Utilities;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps.Core.SurfaceGate;

internal sealed class SurfaceGateBarrierController
{
    private static readonly Vector3 ManualButtonPosition = new(-17.25f, 291.60f, -36.89f);

    private readonly float _positionToleranceSq;
    private SchematicObject _barrier;
    private Vector3 _closedPosition;

    public SurfaceGateBarrierController(float positionToleranceSq)
    {
        _positionToleranceSq = positionToleranceSq;
    }

    public void SetBarrier(SchematicObject barrier)
    {
        _barrier = barrier;
        _closedPosition = barrier.Position;
    }

    public void Clear()
    {
        _barrier = null;
        _closedPosition = default;
    }

    public void HandleTeamVehicleSpawn(SpawningTeamVehicleEventArgs ev)
    {
        if (ev.Team.TargetFaction != Faction.FoundationEnemy || _barrier is null)
            return;

        Timing.CallDelayed(2.25f, () => PlayBarrierAnimation(22f));
    }

    public void HandleManualButton(Vector3 toyPosition)
    {
        if (_barrier is null || Vector3.SqrMagnitude(toyPosition - ManualButtonPosition) > _positionToleranceSq)
            return;

        PlayBarrierAnimation(3f);
    }

    private void PlayBarrierAnimation(float waitTime)
    {
        if (_barrier is null)
            return;

        Timing.RunCoroutine(AnimateBarrier(waitTime));
    }

    private IEnumerator<float> AnimateBarrier(float waitTime)
    {
        if (_barrier is null || Round.IsLobby || Round.IsEnded)
            yield break;

        yield return Timing.WaitUntilDone(SchematicMover.Move(_barrier, _closedPosition, new Vector3(0, 4f, 0), 0.8f));

        if (Round.IsLobby || Round.IsEnded || _barrier?.transform == null)
            yield break;

        yield return Timing.WaitForSeconds(waitTime);

        if (Round.IsLobby || Round.IsEnded || _barrier?.transform == null)
            yield break;

        yield return Timing.WaitUntilDone(SchematicMover.Move(
            _barrier,
            _closedPosition + new Vector3(0f, 4f, 0f),
            new Vector3(0, -4f, 0),
            1.5f));
    }
}
