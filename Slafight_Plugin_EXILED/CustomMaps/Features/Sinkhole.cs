using System;
using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Hazards;
using Exiled.Events.EventArgs.Player;
using MEC;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Server = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.CustomMaps.Features;

public class Sinkhole : IBootstrapHandler, IDisposable
{
    public static Sinkhole Instance { get; private set; }
    public static void Register()
    {
        Unregister();
        Instance = new();
    }

    public static void Unregister()
    {
        Instance?.Dispose();
        Instance = null;
    }

    private bool _disposed;

    public Sinkhole()
    {
        Server.RoundStarted += RoundStartHole;
        Exiled.Events.Handlers.Player.Left += OnLeft;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Server.RoundStarted -= RoundStartHole;
        Exiled.Events.Handlers.Player.Left -= OnLeft;
        if (_sinkholeHandle.IsRunning)
            Timing.KillCoroutines(_sinkholeHandle);
        Sinkholes.Clear();
        JoiningPlayers.Clear();
        GC.SuppressFinalize(this);
    }
    
    private readonly List<Vector3> Sinkholes = [];
    private readonly List<Player> JoiningPlayers = [];
    private CoroutineHandle _sinkholeHandle;
    private List<CTeam> DistargetTeams =
    [
        CTeam.SCPs
    ];

    private List<CRoleTypeId> DistargetRoles =
    [
        CRoleTypeId.SergeyMakarovAwaken,
        CRoleTypeId.Sculpture,
        CRoleTypeId.Scp3125,
        CRoleTypeId.FifthistMarionette
    ];
    
    private void RoundStartHole()
    {
        Sinkholes.Clear();
        JoiningPlayers.Clear();

        if (_sinkholeHandle.IsRunning)
            Timing.KillCoroutines(_sinkholeHandle);

        _sinkholeHandle = Timing.RunCoroutine(SinkholesCoroutine());
    }

    private void OnLeft(LeftEventArgs ev)
    {
        if (ev.Player == null)
            return;

        JoiningPlayers.RemoveAll(player => player?.ReferenceHub == null || player.Id == ev.Player.Id);
    }

    private IEnumerator<float> SinkholesCoroutine()
    {
        for (;;)
        {
            if (Round.IsLobby || Round.IsEnded)
                yield break;

            JoiningPlayers.RemoveAll(player => player?.ReferenceHub == null);

            Sinkholes.Clear();
            foreach (var hazard in Hazard.List)
            {
                if (hazard.Type == HazardType.Sinkhole)
                    Sinkholes.Add(hazard.Position);
            }

            foreach (var player in Player.List)
            {
                // プレイヤー生存・接続チェック
                if (player?.ReferenceHub == null || !player.IsAlive)
                    continue;

                if (DistargetTeams.Contains(player.GetTeam()) || player.GetCustomRole() == CRoleTypeId.SergeyMakarovAwaken || player.GetCustomRole() == CRoleTypeId.Sculpture)
                    continue;

                foreach (var sinkhole in Sinkholes)
                {
                    float distance = Vector3.Distance(player.Position, sinkhole);
                    if (distance <= 1.5f)
                    {
                        if (!JoiningPlayers.Contains(player))
                        {
                            SpeakerApi.Play("SinkholeFall.ogg", "Sinkhole", player.Position, true, null, false, 10, 0);
                            JoiningPlayers.Add(player);
                            player.IsGodModeEnabled = true;

                            Timing.RunCoroutine(PocketJoinAnim(player, sinkhole));

                            Timing.CallDelayed(3.1f, () =>
                            {
                                if (player?.ReferenceHub == null)
                                    return;

                                player.EnableEffect(EffectType.PocketCorroding);
                                JoiningPlayers.Remove(player);

                                Timing.CallDelayed(0.15f, () =>
                                {
                                    if (player?.ReferenceHub == null)
                                        return;

                                    player.IsGodModeEnabled = false;
                                });
                            });
                        }
                    }
                }
            }

            yield return Timing.WaitForSeconds(3f);
        }
    }
    
    private IEnumerator<float> PocketJoinAnim(Player player, Vector3 sinkholePos)
    {
        float elapsedTime = 0f;
        const float totalDuration = 3f;

        Vector3 startPos = player.Position;
        Vector3 endPos = sinkholePos + new Vector3(0f, -1.05f, 0f);

        while (elapsedTime < totalDuration)
        {
            if (Round.IsLobby || Round.IsEnded)
                yield break;

            if (player?.ReferenceHub == null || !player.IsAlive)
                yield break;

            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / totalDuration;
            player.Position = Vector3.Lerp(startPos, endPos, progress);

            yield return 0f;
        }
    }
}
