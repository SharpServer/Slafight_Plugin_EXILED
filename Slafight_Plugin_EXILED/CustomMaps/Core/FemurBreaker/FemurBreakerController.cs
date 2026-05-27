using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps.Core.Utilities;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps.Core.FemurBreaker;

internal sealed class FemurBreakerController
{
    private readonly float _joinRadiusSq;
    private readonly float _buttonToleranceSq;
    private readonly List<Player> _capturedPlayers = [];
    private CoroutineHandle _monitoringCoroutine;
    private SchematicObject _door;
    private SchematicObject _button;

    public bool HasCapturedVictim { get; private set; }

    public bool IsActivated { get; private set; }

    public FemurBreakerController(
        float joinRadiusSq,
        float buttonToleranceSq)
    {
        _joinRadiusSq = joinRadiusSq;
        _buttonToleranceSq = buttonToleranceSq;
    }

    public void SetDoor(SchematicObject door)
    {
        _door = door;
    }

    public void SetButton(SchematicObject button)
    {
        _button = button;
    }

    public void StartMonitoringIfReady()
    {
        StopMonitoring();

        if (MapFlags.FemurBreakerJoinPoint == default ||
            MapFlags.FemurBreakerCapybaraPoint == default)
        {
            return;
        }

        _monitoringCoroutine = Timing.RunCoroutine(MonitorJoinPoint());
    }

    public void HandleButton(Vector3 toyPosition, LabApi.Features.Wrappers.Player labPlayer)
    {
        if (_button == null || Vector3.SqrMagnitude(toyPosition - _button.Position) > _buttonToleranceSq)
            return;

        Activate(labPlayer);
    }

    public void StopMonitoring()
    {
        if (_monitoringCoroutine.IsRunning)
            Timing.KillCoroutines(_monitoringCoroutine);
    }

    public void ResetState()
    {
        HasCapturedVictim = false;
        IsActivated = false;
        _capturedPlayers.Clear();
    }

    public void Clear()
    {
        StopMonitoring();
        ResetState();
        _door = null;
        _button = null;
    }

    private void Activate(LabApi.Features.Wrappers.Player labPlayer)
    {
        var player = Player.Get(labPlayer.NetworkId);

        if (!HasCapturedVictim || IsActivated)
        {
            player?.ShowHint("準備が完了していないか、既に実行されています。");
            return;
        }

        IsActivated = true;
        KillCapturedPlayers();
        ScheduleScp106Recontainment();
        SpeakerApi.Play("FemurBreaker.ogg", "FemurBreaker", Vector3.zero, true, null, false, 999999999, 0);
        Timing.CallDelayed(28f, AnnounceResult);
    }

    private void KillCapturedPlayers()
    {
        foreach (var player in _capturedPlayers.ToList())
        {
            if (player?.IsConnected == true)
                player.Kill("Femur Breakerの犠牲となった");
        }
    }

    private void ScheduleScp106Recontainment()
    {
        foreach (var scp106 in GetScp106Players())
        {
            var captured = scp106;
            Timing.CallDelayed(28f, () =>
            {
                if (captured?.IsConnected == true && HasCapturedVictim && IsActivated)
                    captured.Kill("Femur Breakerによって再収容された");
            });
        }
    }

    private void AnnounceResult()
    {
        if (!HasCapturedVictim || !IsActivated)
            return;

        bool hasScp106StillConnected = GetScp106Players().Any();
        if (hasScp106StillConnected)
        {
            Exiled.API.Features.Cassie.MessageTranslated(
                "SCP 1 0 6 recontained successfully by femur breaker",
                "<color=red>SCP-106</color>のFEMUR BREAKERによる再収容に成功しました。");
            return;
        }

        Exiled.API.Features.Cassie.MessageTranslated(
            "Femur Breaker Process Successfully Completed. but no effect for containment breach.",
            "FEMUR BREAKERプロセスが正常に完了しましたが、収容違反への影響が確認されませんでした。");
    }

    private static IEnumerable<Player> GetScp106Players()
    {
        return Player.List.Where(player =>
            player?.IsConnected == true &&
            (player.GetCustomRole() == CRoleTypeId.Scp106 ||
             player.GetCustomRole() == CRoleTypeId.None && player.Role.Type == RoleTypeId.Scp106));
    }

    private IEnumerator<float> MonitorJoinPoint()
    {
        while (true)
        {
            if (!Round.InProgress)
            {
                ResetState();
                yield break;
            }

            var target = Player.List.FirstOrDefault(player =>
                player.IsConnected &&
                player.GetTeam() != CTeam.SCPs &&
                Vector3.SqrMagnitude(player.Position - MapFlags.FemurBreakerJoinPoint) <= _joinRadiusSq);

            if (target != null)
            {
                CaptureVictim(target);
                yield break;
            }

            yield return Timing.WaitForSeconds(0.5f);
        }
    }

    private void CaptureVictim(Player target)
    {
        target.Handcuff();
        target.Position = MapFlags.FemurBreakerCapybaraPoint;
        _capturedPlayers.Add(target);
        HasCapturedVictim = true;

        if (_door != null)
            Timing.RunCoroutine(SchematicMover.Move(_door, _door.Position, new Vector3(0f, -2.5f, 0f), 0.65f));
    }
}
