using System.Collections.Generic;
using Exiled.API.Features;
using MEC;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.CustomMaps;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Slafight_Plugin_EXILED.CustomMaps.Features;

public static class TrainComing
{
    // 進行中のコルーチンハンドル管理
    private static CoroutineHandle _mainLoopHandle;
    private static CoroutineHandle _firstAnimHandle;
    private static CoroutineHandle _secondAnimHandle;

    // 直近の列車オブジェクト参照（クリーンアップ用）
    private static SchematicObject _currentTrain;

    // 最後に指定された座標（引数なし Start() 用）
    private static Vector3 _lastStartPos;
    private static Vector3 _lastCheckpointPos;
    private static Vector3 _lastEndPos;

    /// <summary>
    /// メインループ開始。Plugin 側から 1 回だけ呼ぶ想定。
    /// </summary>
    public static void Start(Vector3 startPos, Vector3 checkpointPos, Vector3 endPos)
    {
        // 座標を記録しておく（引数なし版で再利用）
        _lastStartPos      = startPos;
        _lastCheckpointPos = checkpointPos;
        _lastEndPos        = endPos;

        // 既に走っていたら殺す
        StopAll();

        _mainLoopHandle = Timing.RunCoroutine(
            SpawnTrainAndAnim(startPos, checkpointPos, endPos),
            Segment.Update,
            "TrainMainLoop"
        );
    }

    /// <summary>
    /// 前回と同じ座標でメインループを再開する。
    /// Start(Vector3...) を一度も呼んでいない場合は警告を出して何もしない。
    /// </summary>
    public static void Start()
    {
        if (MapFlags.TrainStartPoint == default &&
            MapFlags.TrainCheckpointPoint == default &&
            MapFlags.TrainEndPoint == default)
        {
            Log.Warn("[Train] Start() called but no coordinates have been set yet. Call Start(Vector3, Vector3, Vector3) first.");
            return;
        }

        Start(_lastStartPos, _lastCheckpointPos, _lastEndPos);
    }

    /// <summary>
    /// ラウンド終了・プラグイン無効化時などに呼んで完全停止。
    /// </summary>
    public static void StopAll()
    {
        if (_mainLoopHandle.IsRunning)
            Timing.KillCoroutines(_mainLoopHandle);

        if (_firstAnimHandle.IsRunning)
            Timing.KillCoroutines(_firstAnimHandle);

        if (_secondAnimHandle.IsRunning)
            Timing.KillCoroutines(_secondAnimHandle);

        _mainLoopHandle = default;
        _firstAnimHandle = default;
        _secondAnimHandle = default;

        // 列車残骸があればここで片付け
        if (_currentTrain != null)
        {
            try
            {
                _currentTrain.Destroy();
                Log.Info("[Train] Cleaned up remaining STrain in StopAll.");
            }
            catch (System.Exception ex)
            {
                Log.Error($"[Train] Error destroying remaining STrain in StopAll: {ex}");
            }
            finally
            {
                _currentTrain = null;
            }
        }
    }

    public static IEnumerator<float> SpawnTrainAndAnim(Vector3 startPos, Vector3 checkpointPos, Vector3 endPos)
    {
        for (;;)
        {
            // ラウンド状態チェック
            if (Round.IsLobby || Round.IsEnded)
            {
                Log.Info("[Train] Round is lobby/ended, stop main loop.");
                StopAll();
                yield break;
            }

            if (!ObjectSpawner.TrySpawnSchematic("STrain", startPos, out var train))
            {
                Log.Error("[Train] Failed to spawn STrain.");
                yield break;
            }

            _currentTrain = train;
            Log.Info("[Train] Spawned STrain at " + startPos);

            // 1本目のアニメーション
            _firstAnimHandle = Timing.RunCoroutine(
                AnimSafe(train, startPos, checkpointPos, 3.015f),
                Segment.Update,
                "TrainFirstAnim"
            );

            // かなり余裕を持った待機。途中でラウンド終了したら抜ける。
            float wait = 35f;
            while (wait > 0f)
            {
                if (Round.IsLobby || Round.IsEnded)
                {
                    Log.Info("[Train] Round ended during first anim wait, stopping.");
                    StopAll();
                    yield break;
                }

                wait -= Time.deltaTime;
                yield return 0f;
            }

            // 2本目のアニメーション（同様にハンドル管理）
            _secondAnimHandle = Timing.RunCoroutine(
                AnimSafe(train, checkpointPos, endPos, 1.765f),
                Segment.Update,
                "TrainSecondAnim"
            );

            // 念のため少し待つ（同じくラウンド状態を見る）
            wait = 2f;
            while (wait > 0f)
            {
                if (Round.IsLobby || Round.IsEnded)
                {
                    Log.Info("[Train] Round ended during second anim wait, stopping.");
                    StopAll();
                    yield break;
                }

                wait -= Time.deltaTime;
                yield return 0f;
            }

            // Destroy 前にアニメーションコルーチンを止める
            if (_firstAnimHandle.IsRunning)
                Timing.KillCoroutines(_firstAnimHandle);
            if (_secondAnimHandle.IsRunning)
                Timing.KillCoroutines(_secondAnimHandle);

            _firstAnimHandle = default;
            _secondAnimHandle = default;

            // Destroy 時も null チェック＋ try/catch
            try
            {
                if (train != null)
                {
                    train.Destroy();
                    Log.Info("[Train] Destroyed STrain, scheduling next.");
                }
                else
                {
                    Log.Warn("[Train] Destroy skipped: train was already null.");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[Train] Error destroying STrain: {ex}");
            }
            finally
            {
                if (_currentTrain == train)
                    _currentTrain = null;
            }

            // 次の列車までランダム待機
            float delay = 50f * Random.Range(0f, 5.000001f);
            while (delay > 0f)
            {
                if (Round.IsLobby || Round.IsEnded)
                {
                    Log.Info("[Train] Round ended during next-train delay, stopping.");
                    StopAll();
                    yield break;
                }

                delay -= Time.deltaTime;
                yield return 0f;
            }
        }
    }

    // 安全版アニメーション
    private static IEnumerator<float> AnimSafe(SchematicObject schem, Vector3 startpos, Vector3 endpos, float duration)
    {
        if (schem is null || duration <= 0f)
        {
            Log.Warn("[Train] AnimSafe aborted: schem is null or duration <= 0.");
            yield break;
        }

        float elapsedTime = 0f;
        Vector3 startPos = startpos;
        Vector3 endPos = endpos;

        while (elapsedTime < duration)
        {
            // ラウンド終了・ロビー移行なら即終了
            if (Round.IsLobby || Round.IsEnded)
            {
                Log.Info("[Train] AnimSafe stopped: round lobby/ended.");
                yield break;
            }

            // Destroy 済み対策
            if (schem == null || schem.transform == null)
            {
                Log.Warn("[Train] AnimSafe stopped: schem or its transform is null.");
                yield break;
            }

            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / duration;
            schem.transform.position = Vector3.Lerp(startPos, endPos, progress);

            yield return 0f;
        }

        if (schem != null && schem.transform != null)
        {
            schem.transform.position = endPos;
        }
        else
        {
            Log.Warn("[Train] AnimSafe end: schem destroyed before setting final position.");
        }
    }
}
