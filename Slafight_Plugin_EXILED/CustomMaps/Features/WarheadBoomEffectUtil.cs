using System;
using System.Collections.Generic;
using MEC;
using Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;
using UnityEngine;
using Random = System.Random;

namespace Slafight_Plugin_EXILED.CustomMaps.Features;

public static class WarheadBoomEffectUtil
{
    // 生成範囲の半径 (position を中心にランダムオフセット)
    private const float SpawnRadiusXZ = 5.5f;

    // スケールのランダム幅
    private const float ScaleMin = 0.9f;
    private const float ScaleMax = 1.1f;

    private static readonly List<CoroutineHandle> CoroutineHandles = [];
    private static readonly Random         Rng              = new();

    /// <summary>
    /// 指定座標に煙エフェクトを <paramref name="endTime"/> 秒間、
    /// <paramref name="spawnInterval"/> 間隔で生成し続ける。
    /// </summary>
    public static void CreateAndStartEffect(
        Vector3 position,
        float   endTime,
        float   spawnInterval            = 0.5f,
        float   spawnIntervalRandomRange = 0.05f)
    {
        CoroutineHandle handle = default;
        handle = Timing.RunCoroutine(
            Coroutine(handle, position, endTime, spawnInterval, spawnIntervalRandomRange));
        CoroutineHandles.Add(handle);
    }

    /// <summary>実行中の全エフェクトコルーチンを停止する。</summary>
    public static void StopAllEffects()
    {
        foreach (var h in CoroutineHandles)
        {
            if (h.IsRunning)
                Timing.KillCoroutines(h);
        }
        CoroutineHandles.Clear();
    }

    // ================================================================
    //  内部コルーチン
    // ================================================================
    private static IEnumerator<float> Coroutine(
        CoroutineHandle handle,
        Vector3 position,
        float   endTime,
        float   spawnInterval,
        float   spawnIntervalRandomRange)
    {
        float elapsed = 0f;

        while (elapsed < endTime)
        {
            SpawnOneSmokeObject(position);

            float waitTime = spawnInterval
                + (float)((Rng.NextDouble() * 2.0 - 1.0) * spawnIntervalRandomRange);
            waitTime = Mathf.Max(0.05f, waitTime);

            elapsed += waitTime;
            yield return Timing.WaitForSeconds(waitTime);
        }

        CoroutineHandles.Remove(handle);
    }

    // ================================================================
    //  ヘルパー: 煙オブジェクト1つをスポーン
    // ================================================================
    private static void SpawnOneSmokeObject(Vector3 basePosition)
    {
        // XZ 平面でランダムオフセット (円形分布)
        float angle  = (float)(Rng.NextDouble() * Math.PI * 2.0);
        float radius = (float)(Rng.NextDouble() * SpawnRadiusXZ);
        var spawnPos = basePosition + new Vector3(
            Mathf.Cos(angle) * radius,
            0f,
            Mathf.Sin(angle) * radius
        );

        var   rotation = Quaternion.Euler(0f, (float)(Rng.NextDouble() * 360.0), 0f);
        float scale    = (float)(Rng.NextDouble() * (ScaleMax - ScaleMin) + ScaleMin);

        // Position/Rotation/Scale を設定してから Create() — Tentacle と同じパターン
        var effect = new WarheadBoomEffect
        {
            Position = spawnPos,
            Rotation = rotation,
            Scale    = Vector3.one * scale
        };

        if (effect.Create() is WarheadBoomEffect created)
        {
            created.StartSmoke();
        }
    }
}