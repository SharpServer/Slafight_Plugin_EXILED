using System;
using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class DninoueMissile : CItemKeycard
{
    public override string DisplayName => "にゃあ";
    public override string Description => "あああああああああああああああああああああああああああ";

    protected override string UniqueKey => "DninoueMissile";
    protected override ItemType BaseItem => ItemType.KeycardCustomTaskForce;

    protected override string KeycardLabel => "ADMIN ULTIMATE TOOL";
    protected override Color32? KeycardLabelColor => new Color32(255, 0, 250, 255);
    protected override string KeycardName => "UwU. 55555";
    protected override Color32? TintColor => new Color32(255, 0, 250, 255);
    protected override Color32? KeycardPermissionsColor => new Color32(255, 255, 255, 255);
    protected override KeycardPermissions Permissions => KeycardPermissions.None;
    protected override byte Rank => 1;
    protected override string SerialNumber => "555555555555";

    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.magenta;

    protected override void OnDropping(DroppingItemEventArgs ev)
    {
        ev.IsAllowed = false;
        ev.Item.Destroy();

        try
        {
            var schem = ObjectSpawner.SpawnSchematic("YoungDevLikesUltimatePicture", ev.Player.Position, ev.Player.CameraTransform.forward);
            Timing.RunCoroutine(MissileCoroutine(schem, ev.Player));
        }
        catch (Exception)
        {
            Log.Error("Stupid Null Error.");
        }
    }

    private static IEnumerator<float> MissileCoroutine(SchematicObject schem, Player pushPlayer)
    {
        if (schem == null || schem.transform == null)
        {
            Log.Warn("[MagicMissile] MissileCoroutine aborted: schem or transform is null at start.");
            yield break;
        }

        float elapsedTime = 0f;
        const float totalDuration = 15.5f;

        Vector3 startPos = schem.transform.position;
        Vector3 cameraForward = pushPlayer != null ? pushPlayer.CameraTransform.forward.normalized : Vector3.forward;
        Vector3 endPos = startPos + cameraForward * 1.55555f;

        while (elapsedTime < totalDuration)
        {
            if (Round.IsLobby || Round.IsEnded) yield break;
            if (schem == null || schem.transform == null) yield break;
            if (pushPlayer != null && !pushPlayer.IsConnected) yield break;

            foreach (var player in Player.List)
            {
                if (player == null || !player.IsConnected || !player.IsAlive) continue;

                if (Vector3.Distance(schem.transform.position, player.Transform.position) <= 2.5f)
                {
                    try
                    {
                        player.Heal(10f, true);
                        player.EnableEffect(EffectType.Invigorated, 255, 5f);
                        player.EnableEffect(EffectType.Ghostly, 255, 5f);
                        player.EnableEffect(EffectType.DamageReduction, 255, 5f);
                        pushPlayer?.ShowHitMarker();
                        SpeakerApi.Play("healsound.ogg", "Nyaaaaa", player.Position, true, schem.transform, false, 5f, 0f);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[DninoueMissile] Heal error: {ex}");
                    }
                }
            }

            elapsedTime += Time.deltaTime;
            schem.transform.position = Vector3.Lerp(startPos, endPos, elapsedTime / totalDuration);

            yield return 0f;
        }

        try { schem?.Destroy(); }
        catch (Exception ex) { Log.Error($"[MagicMissile] Error destroying schem: {ex}"); }
    }
}
