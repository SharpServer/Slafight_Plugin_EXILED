using System;
using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Scp1509;
using MEC;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class ThrowKnife : CItem
{
    public override string DisplayName => "投げナイフ";
    public override string Description => "W.I.P";
    protected override string UniqueKey => "ThrowKnife";
    protected override ItemType BaseItem => ItemType.SCP1509;
    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.red;

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Scp1509.Resurrecting += OnResurrecting;
        Exiled.Events.Handlers.Scp1509.TriggeringAttack += OnTriggeringAttack;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Scp1509.Resurrecting -= OnResurrecting;
        Exiled.Events.Handlers.Scp1509.TriggeringAttack -= OnTriggeringAttack;
        base.UnregisterEvents();
    }

    private void OnResurrecting(ResurrectingEventArgs ev)
    {
        if (!Check(ev.Item)) return;
        ev.IsAllowed = false;
    }

    private void OnTriggeringAttack(TriggeringAttackEventArgs ev)
    {
        if (!Check(ev.Item) || ev.Player is null) return;
        ev.IsAllowed = false;
        var startPos = ev.Player.Position + new Vector3(0f, 0.5f, 0f);
        try
        {
            var schem = ObjectSpawner.SpawnSchematic("ThrowKnife", startPos, ev.Player.CameraTransform.forward);
            Timing.RunCoroutine(AnimCoroutine(schem, ev.Player));
        }
        catch (Exception)
        {
            // ignored
        }
        ev.Item.Destroy();
    }

    private static IEnumerator<float> AnimCoroutine(SchematicObject schem, Player pushPlayer)
    {
        // 開始時点チェック
        if (schem == null || schem.transform == null)
        {
            yield break;
        }

        float elapsedTime = 0f;
        const float totalDuration = 0.8f;

        Vector3 startPos = schem.transform.position;
        Vector3 cameraForward = pushPlayer != null
            ? pushPlayer.CameraTransform.forward.normalized
            : Vector3.forward;
        Vector3 endPos = startPos + cameraForward * 25f + new Vector3(0f, 0.15f, 0f);
        HashSet<int> hitPlayers = [];

        while (elapsedTime < totalDuration)
        {
            // ラウンド状態
            if (Round.IsLobby || Round.IsEnded)
                break;

            // Schematic 消滅
            if (schem == null || schem.transform == null)
                break;

            // 発射主 disconnect
            if (!pushPlayer.IsConnected)
                break;

            // 当たり判定
            foreach (var player in Player.List)
            {
                if (player == null || !player.IsConnected || !player.IsAlive || player.GetTeam() is CTeam.SCPs)
                    continue;

                if (!(Vector3.Distance(schem.transform.position, player.Transform.position) <= 1f)) continue;
                if (player == pushPlayer) continue;
                if (!hitPlayers.Add(player.Id)) continue;

                try
                {
                    player.EnableEffect<Bleeding>(20, 15f);
                    player.Hurt(pushPlayer, 50f, DamageType.Scp1509, null, "ナイフにぶっ飛ばされた");
                    pushPlayer?.ShowHitMarker();
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            // 移動
            elapsedTime += Time.deltaTime;
            var progress = elapsedTime / totalDuration;
            schem.transform.position = Vector3.Lerp(startPos, endPos, progress);

            yield return 0f;
        }

        if (schem != null)
        {
            try
            {
                schem.Destroy();
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }
}
