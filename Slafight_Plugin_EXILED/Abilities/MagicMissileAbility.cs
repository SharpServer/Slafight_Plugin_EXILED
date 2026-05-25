using System;
using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using MEC;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomRoles.Others.SergeyMakarov;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Abilities;

public class MagicMissileAbility : AbilityBase
{
    // AbilityBase 抽象プロパティの実装（デフォルト値）
    protected override float DefaultCooldown => 5f;
    protected override int DefaultMaxUses => -1;

    // 完全デフォルト
    public MagicMissileAbility(Player owner)
        : base(owner) { }

    // クールダウンだけ変える
    public MagicMissileAbility(Player owner, float cooldownSeconds)
        : base(owner, cooldownSeconds, null) { }

    // 両方カスタム
    public MagicMissileAbility(Player owner, float cooldownSeconds, int maxUses)
        : base(owner, cooldownSeconds, maxUses) { }

    protected override void ExecuteAbility(Player player)
    {
        var startPos = player.Position + new Vector3(0f, 0.5f, 0f);
        try
        {
            var schem = ObjectSpawner.SpawnSchematic("SCP3005", startPos, player.CameraTransform.forward);
            Timing.RunCoroutine(MissileCoroutine(schem, player));
        }
        catch (Exception ex)
        {
            Log.Error("MagicMissile spawn failed: " + ex.Message);
        }
    }

    private static IEnumerator<float> MissileCoroutine(SchematicObject schem, Player pushPlayer)
    {
        // 開始時点チェック
        if (schem == null || schem.transform == null)
        {
            Log.Warn("[MagicMissileAbility] MissileCoroutine aborted: schem or transform is null at start.");
            yield break;
        }

        float elapsedTime = 0f;
        const float totalDuration = 0.8f;

        Vector3 startPos = schem.transform.position;
        Vector3 cameraForward = pushPlayer != null
            ? pushPlayer.CameraTransform.forward.normalized
            : Vector3.forward;
        Vector3 endPos = startPos + cameraForward * 25f + new Vector3(0f, 0.15f, 0f);

        while (elapsedTime < totalDuration)
        {
            // ラウンド状態
            if (Round.IsLobby || Round.IsEnded)
            {
                Log.Info("[MagicMissileAbility] MissileCoroutine stopped: round lobby/ended.");
                yield break;
            }

            // Schematic 消滅
            if (schem == null || schem.transform == null)
            {
                Log.Warn("[MagicMissileAbility] MissileCoroutine stopped: schem destroyed.");
                yield break;
            }

            // 発射主 disconnect
            if (pushPlayer != null && !pushPlayer.IsConnected)
            {
                Log.Info("[MagicMissileAbility] MissileCoroutine stopped: owner disconnected.");
                yield break;
            }

            // 当たり判定
            foreach (var player in Player.List)
            {
                if (player == null || !player.IsConnected || !player.IsAlive)
                    continue;

                if (!(Vector3.Distance(schem.transform.position, player.Transform.position) <= 1f)) continue;
                if (player == pushPlayer) continue;
                try
                {
                    player.EnableEffect<Burned>(255, 35);
                    player.EnableEffect<Concussed>(255, 35);
                    player.EnableEffect<Asphyxiated>(1, 35);
                    player.Hurt(pushPlayer, 10f, DamageType.Unknown, null,
                        !pushPlayer.IsSergeyMarkov()
                            ? "<color=#ff00fa>第五的</color>な力による影響"
                            : "<color=red><b>怨念的</b></color>な力による影響");
                    pushPlayer?.ShowHitMarker();
                }
                catch (Exception ex)
                {
                    Log.Error($"[MagicMissileAbility] Hurt error: {ex}");
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
            catch (Exception ex)
            {
                Log.Error($"[MagicMissileAbility] Error destroying schem: {ex}");
            }
        }
    }
}