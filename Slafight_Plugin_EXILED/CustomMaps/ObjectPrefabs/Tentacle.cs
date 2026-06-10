using System.Collections.Generic;
using Exiled.API.Features;
using MEC;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

public class Tentacle : ObjectPrefab
{
    public override Vector3 Position
    {
        get => _schematicObject != null ? _schematicObject.Position : base.Position;
        set
        {
            if (_isDestroying)
                return;

            if (_schematicObject != null)
                _schematicObject.Position = value;
            else
                base.Position = value;
        }
    }

    public override Quaternion Rotation
    {
        get => _schematicObject != null ? _schematicObject.Rotation : base.Rotation;
        set
        {
            if (_isDestroying)
                return;

            if (_schematicObject != null)
                _schematicObject.Rotation = value;
            else
                base.Rotation = value;
        }
    }

    public override Vector3 Scale
    {
        get => _schematicObject != null ? _schematicObject.Scale : base.Scale;
        set
        {
            if (_isDestroying)
                return;

            if (_schematicObject != null)
                _schematicObject.Scale = value;
            else
                base.Scale = value;
        }
    }

    private SchematicObject _schematicObject;
    private CoroutineHandle _coroutineHandle;
    private bool _isDestroying;

    protected override void OnCreate()
    {
        // Schematic の spawn に失敗した場合に備えたチェックも入れておく
        _schematicObject = ObjectSpawner.SpawnSchematic("Tentacle", base.Position, base.Rotation);

        if (_schematicObject == null)
        {
            Log.Error("[Tentacle] Failed to spawn schematic 'Tentacle'.");
            return;
        }

        _coroutineHandle = Timing.RunCoroutine(TentacleCoroutine());
        base.OnCreate();
    }

    protected override void OnDestroy()
    {
        if (_isDestroying)
            return;

        _isDestroying = true;

        // コルーチンを確実に止める
        if (_coroutineHandle.IsRunning)
            Timing.KillCoroutines(_coroutineHandle);

        // ローカルに退避してキャプチャ。_schematicObject 自体は後で null にする。
        var schematic = _schematicObject;

        // 破棄前の「destroying」アニメを安全に再生
        if (schematic != null)
        {
            try
            {
                var anim = schematic.AnimationController;   // ここで AttachedBlocks を触るので try/catch 必須
                if (anim != null)
                    anim.Play("destroying");
            }
            catch
            {
                Log.Debug("[Tentacle] Exception while playing 'destroying' animation (pre-delay).");
            }
        }

        // 遅延してから Stop + Destroy
        Timing.CallDelayed(1.05f, () =>
        {
            if (schematic != null)
            {
                try
                {
                    var anim = schematic.AnimationController;
                    if (anim != null)
                        anim.Stop();
                }
                catch
                {
                    Log.Debug("[Tentacle] Exception while stopping animation (post-delay).");
                }

                try
                {
                    schematic.Destroy();  // SchematicObject.Destroy -> GameObject.Destroy + NetworkServer.Destroy + イベント発火
                }
                catch
                {
                    Log.Debug("[Tentacle] Exception in schematic.Destroy().");
                }
            }

            _schematicObject = null;
            Log.Debug("[Tentacle] Destroy Schematic called.");
        });

        base.OnDestroy();
    }

    private IEnumerator<float> TentacleCoroutine()
    {
        // 生成直後の失敗を考慮
        if (_schematicObject == null)
            yield break;

        AnimationController animator = null;

        try
        {
            animator = _schematicObject.AnimationController;
        }
        catch
        {
            Log.Debug("[Tentacle] Failed to get AnimationController at coroutine start.");
            yield break;
        }

        if (animator == null)
            yield break;

        // 生成アニメ
        try
        {
            animator.Play("spawning");
        }
        catch
        {
            Log.Debug("[Tentacle] Exception while playing 'spawning' animation.");
            yield break;
        }

        yield return Timing.WaitForSeconds(1f);

        while (true)
        {
            // destroy 中 or schematic 破棄後ならコルーチン終了
            if (_isDestroying || _schematicObject == null || animator == null)
                yield break;

            try
            {
                animator.Play("idle");
            }
            catch
            {
                Log.Debug("[Tentacle] Exception while playing 'idle' animation.");
                yield break;
            }

            yield return Timing.WaitForSeconds(5f);

            if (_isDestroying || _schematicObject == null || animator == null)
                yield break;

            Player targetPlayer = null;

            // 近くのプレイヤー探索
            foreach (var player in Player.List)
            {
                if (player == null || player.GetTeam() == CTeam.SCPs)
                    continue;

                if (Vector3.Distance(player.Position, Position) <= 3f)
                {
                    targetPlayer = player;
                    break;
                }
            }

            if (targetPlayer == null)
                continue;

            // 攻撃アニメ開始
            try
            {
                animator.Play("attacking");
            }
            catch
            {
                Log.Debug("[Tentacle] Exception while playing 'attacking' animation.");
                yield break;
            }

            // 攻撃アニメ中の追従
            const float attackWindow = 0.83f;      // 約 50 frame
            const float checkInterval = 1f / 60f;  // 1 frame 相当
            float elapsed = 0f;

            while (elapsed < attackWindow)
            {
                if (_isDestroying || _schematicObject == null || animator == null)
                    yield break;

                if (targetPlayer != null && targetPlayer.IsAlive)
                {
                    var toTarget = targetPlayer.Position - Position;
                    toTarget.y = 0f;

                    if (toTarget.sqrMagnitude > 0.001f)
                    {
                        var rot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                        Rotation = rot;
                    }
                }

                elapsed += checkInterval;
                yield return Timing.WaitForSeconds(checkInterval);
            }

            // 攻撃判定
            if (_isDestroying || _schematicObject == null || animator == null)
                yield break;

            if (targetPlayer != null &&
                targetPlayer.IsAlive &&
                Vector3.Distance(targetPlayer.Position, Position) <= 3f)
            {
                targetPlayer.Hurt(35, "SCP-035の触手に殺された");
            }
        }
    }
}