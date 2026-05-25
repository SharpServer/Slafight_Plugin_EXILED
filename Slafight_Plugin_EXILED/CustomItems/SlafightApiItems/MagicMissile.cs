using System;
using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomRoles.Others.SergeyMakarov;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class MagicMissile : CItemKeycard
{
    public override string DisplayName => "第五教会 マジックミサイル";
    public override string Description => "非常に第五的なマジックミサイルを発射する。";

    protected override string UniqueKey => "MagicMissile";
    protected override ItemType BaseItem => ItemType.KeycardCustomTaskForce;

    protected override string KeycardLabel => "マジックミサイル";
    protected override Color32? KeycardLabelColor => new Color32(255, 0, 250, 255);
    protected override string KeycardName => "Mgc. Fifth";
    protected override Color32? TintColor => new Color32(255, 0, 250, 255);
    protected override Color32? KeycardPermissionsColor => new Color32(255, 255, 255, 255);
    protected override KeycardPermissions Permissions => KeycardPermissions.None;
    protected override byte Rank => 1;
    protected override string SerialNumber => "555555555555";

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => Color.magenta;

    /// <summary>
    /// 投げる (Drop) でアイテムを消費して SCP3005 schematic を発射、
    /// 軌道上の他プレイヤーに継続ダメージを与える。
    /// </summary>
    protected override void OnDropping(DroppingItemEventArgs ev)
    {
        ev.IsAllowed = false;
        ev.Item.Destroy();

        try
        {
            var schem = ObjectSpawner.SpawnSchematic("SCP3005", ev.Player.Position, ev.Player.CameraTransform.forward);
            Timing.RunCoroutine(MissileCoroutine(schem, ev.Player));
        }
        catch (Exception ex)
        {
            Log.Error($"[MagicMissile] Schematic spawn failed: {ex}");
        }
    }

    private static IEnumerator<float> MissileCoroutine(SchematicObject schem, Player pushPlayer)
    {
        if (schem == null || schem.transform == null) yield break;

        const float totalDuration = 0.8f;
        var elapsed = 0f;
        var startPos = schem.transform.position;
        var forward = pushPlayer != null ? pushPlayer.CameraTransform.forward.normalized : Vector3.forward;
        var endPos = startPos + forward * 25f + new Vector3(0f, 0.15f, 0f);

        while (elapsed < totalDuration)
        {
            if (Round.IsLobby || Round.IsEnded) yield break;
            if (schem == null || schem.transform == null) yield break;
            if (pushPlayer != null && !pushPlayer.IsConnected) yield break;

            foreach (var player in Player.List)
            {
                if (player == null || !player.IsConnected || !player.IsAlive) continue;
                if (player == pushPlayer) continue;
                if (Vector3.Distance(schem.transform.position, player.Transform.position) > 1f) continue;

                try
                {
                    player.EnableEffect<Burned>(255, 15);
                    player.EnableEffect<Concussed>(255, 15);
                    player.EnableEffect<Asphyxiated>(1, 15);
                    player.Hurt(pushPlayer, 10f, DamageType.Unknown, null,
                        !pushPlayer.IsSergeyMarkov()
                            ? "<color=#ff00fa>第五的</color>な力による影響"
                            : "<color=red><b>怨念的</b></color>な力による影響");
                    pushPlayer?.ShowHitMarker();
                }
                catch (Exception ex)
                {
                    Log.Error($"[MagicMissile] Hurt error: {ex}");
                }
            }

            elapsed += Time.deltaTime;
            schem.transform.position = Vector3.Lerp(startPos, endPos, elapsed / totalDuration);
            yield return 0f;
        }

        try { schem?.Destroy(); }
        catch (Exception ex) { Log.Error($"[MagicMissile] Error destroying schem: {ex}"); }
    }
}
