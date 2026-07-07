using System;
using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Toys;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerStatsSystem;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class CapybaraMissile : CItemWeapon
{
    public override string DisplayName => "XXX-CAPYBARA-XXX";
    public override string Description => "HUBERT YEAAAAAAAAA";

    protected override string UniqueKey => "CapybaraMissile";
    protected override ItemType BaseItem => ItemType.GunCOM18;
    protected override float Damage => 0f;
    protected override byte MagazineSize => 5;
    protected override Vector3 Scale => Vector3.one * 5f;

    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.white;

    protected override void OnHurtingOthers(HurtingEventArgs ev)
    {
        if (ev.DamageHandler.Base is FirearmDamageHandler)
            base.OnHurtingOthers(ev);
    }

    protected override void OnShot(ShotEventArgs ev)
    {
        var startPos = ev.Player.Position;
        try
        {
            var cameraForward = ev.Player.CameraTransform.forward.normalized;
            var capybara = Capybara.Create(startPos + cameraForward * 1.5f, Quaternion.Euler(ev.Player.CameraTransform.forward));
            capybara.Collidable = false;
            Timing.RunCoroutine(MissileCoroutine(capybara, ev.Player));
        }
        catch (Exception)
        {
            Log.Error("Stupid Null Error.");
        }
    }

    private static IEnumerator<float> MissileCoroutine(Capybara? schem, Player pushPlayer)
    {
        if (schem == null || schem.Transform == null)
        {
            Log.Warn("[MagicMissile] MissileCoroutine aborted: schem or transform is null at start.");
            yield break;
        }

        float elapsedTime = 0f;
        const float totalDuration = 5f;
        const float rotationSpeed = 1440f;

        Vector3 startPos = schem.Transform.position;
        Vector3 cameraForward = pushPlayer != null ? pushPlayer.CameraTransform.forward.normalized : Vector3.forward;
        Vector3 endPos = startPos + cameraForward * 99f;

        while (elapsedTime < totalDuration)
        {
            if (Round.IsLobby || Round.IsEnded) yield break;
            if (schem == null || schem.Transform == null) yield break;
            if (pushPlayer != null && !pushPlayer.IsConnected) yield break;

            foreach (var player in Player.List)
            {
                if (player == null || !player.IsConnected || !player.IsAlive) continue;

                if (Vector3.Distance(schem.Transform.position, player.Transform.position) <= 1f)
                {
                    try
                    {
                        player.Hurt(pushPlayer, 2f, DamageType.Unknown);
                        pushPlayer?.ShowHitMarker();
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[CapybaraMissile] Attack error: {ex}");
                    }
                }
            }

            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / totalDuration;

            schem.Transform.position = Vector3.Lerp(startPos, endPos, progress);
            schem.Transform.rotation = Quaternion.Euler(0f, rotationSpeed * elapsedTime, 0f);

            yield return 0f;
        }

        try { schem?.Destroy(); }
        catch (Exception ex) { Log.Error($"[MagicMissile] Error destroying schem: {ex}"); }
    }
}
