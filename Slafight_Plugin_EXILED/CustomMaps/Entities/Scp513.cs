using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;
using Utils.NonAllocLINQ;

namespace Slafight_Plugin_EXILED.CustomMaps.Entities;

public static class Scp513
{
    public static void Register()
    {
        Exiled.Events.Handlers.Server.WaitingForPlayers += OnWaitingPlayers;
        Exiled.Events.Handlers.Player.ChangingRole       += OnChangingRole;
        Exiled.Events.Handlers.Player.Left               += OnLeft;
    }

    public static void Unregister()
    {
        Exiled.Events.Handlers.Server.WaitingForPlayers -= OnWaitingPlayers;
        Exiled.Events.Handlers.Player.ChangingRole       -= OnChangingRole;
        Exiled.Events.Handlers.Player.Left               -= OnLeft;
        StalkingTargets.Clear();
        Timing.KillCoroutines(_coroutineHandle);
    }

    private static readonly List<Player> StalkingTargets = [];
    private static CoroutineHandle _coroutineHandle;

    private static void OnWaitingPlayers()
    {
        StalkingTargets.Clear();
        Timing.KillCoroutines(_coroutineHandle);
        _coroutineHandle = Timing.RunCoroutine(Scp513Coroutine());
    }

    private static void OnChangingRole(ChangingRoleEventArgs ev)
    {
        var playerId = ev.Player?.Id ?? 0;
        Timing.CallDelayed(1f, () =>
        {
            if (!ev.IsAllowed) return;
            RemoveTarget(playerId);
        });
    }

    private static void OnLeft(LeftEventArgs ev)
    {
        if (ev.Player != null)
            RemoveTarget(ev.Player.Id);
    }

    public static void AddTarget(Player? player)
    {
        if (player == null) return;
        StalkingTargets.AddIfNotContains(player);
    }

    public static void RemoveTarget(Player? player)
    {
        if (player == null) return;
        RemoveTarget(player.Id);
    }

    private static void RemoveTarget(int playerId)
        => StalkingTargets.RemoveAll(player => player?.ReferenceHub == null || player.Id == playerId);

    private static IEnumerator<float> Scp513Coroutine()
    {
        List<SchematicObject> instances = [];

        while (true)
        {
            if (RoundSummary.SummaryActive)
                yield break;

            // 既存インスタンス破棄
            foreach (var instance in instances)
            {
                if (instance == null) continue;
                instance.NetworkIdentities.RemoveShowState();
                instance.Destroy();
            }
            instances.Clear();

            yield return Timing.WaitForSeconds(Random.Range(8f, 15f));

            StalkingTargets.RemoveAll(player => player?.ReferenceHub == null);
            foreach (var player in StalkingTargets.ToArray())
            {
                if (player?.ReferenceHub == null || !player.IsAlive)
                    continue;

                // プレイヤーの前方7.5m
                Vector3 spawnPos = player.Position + player.Transform.forward * 7.5f;

                // プレイヤーの方を向く（Y回転のみ）
                Vector3 lookDir = player.Position - spawnPos;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude < 0.001f)
                    lookDir = new Vector3(player.Transform.forward.x, 0f, player.Transform.forward.z);

                Quaternion rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);

                var obj = ObjectSpawner.SpawnSchematic("SCP513", spawnPos, rotation);
                if (obj == null) continue;

                obj.transform.SetParent(player.Transform, true);

                // Owner 設定だけで Show/Hide・観戦者同期はすべて Extensions が担う
                obj.NetworkIdentities.InitShowState(new NetworkShowState
                {
                    OwnerId             = player.Id,
                    ShowToOwner         = true,
                    SpectatorVisibility = SpectatorVisibility.Show,
                });

                instances.Add(obj);
            }

            yield return Timing.WaitForSeconds(Random.Range(2f, 7f));
        }
    }
}
