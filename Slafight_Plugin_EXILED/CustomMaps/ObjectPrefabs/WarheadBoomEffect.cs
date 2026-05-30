using System.Collections.Generic;
using MEC;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

public class WarheadBoomEffect : ObjectPrefab
{
    // ---- 煙アニメーション設定 ----
    private static class SmokeConfig
    {
        public const float RiseSpeedMin            = 4.5f;
        public const float RiseSpeedMax            = 8.5f;
        public const float DriftMin                = -3f;
        public const float DriftMax                = 3f;
        public const float DriftChangeIntervalMin  = 0.6f;
        public const float DriftChangeIntervalMax  = 1.4f;
        public const float MaxRiseHeight           = 20f;
        public const float ScaleMultiplierAtMax    = 1.05f;
        public const float TickInterval            = 0.05f;
    }

    // ---- バッキングフィールド ----
    private Vector3    _position = Vector3.zero;
    private Quaternion _rotation = Quaternion.identity;
    private Vector3    _scale    = Vector3.one;

    private SchematicObject? _schematicObject;
    private CoroutineHandle  _smokeCoroutine;
    private Vector3          _spawnPosition;
    private Vector3          _initialScale;
    private bool             _isAnimating;

    private readonly System.Random _rng = new();
    // ================================================================
    //  Position / Rotation / Scale — バッキングフィールド経由で管理
    // ================================================================
    public override Vector3 Position
    {
        get => _schematicObject != null ? _schematicObject.Position : _position;
        set
        {
            _position = value;
            if (_schematicObject != null)
                _schematicObject.Position = value;
        }
    }

    public override Quaternion Rotation
    {
        get => _schematicObject != null ? _schematicObject.Rotation : _rotation;
        set
        {
            _rotation = value;
            if (_schematicObject != null)
                _schematicObject.Rotation = value;
        }
    }

    public override Vector3 Scale
    {
        get => _schematicObject != null ? _schematicObject.Scale : _scale;
        set
        {
            _scale = value;
            if (_schematicObject != null)
                _schematicObject.Scale = value;
        }
    }

    // ================================================================
    //  ライフサイクル
    // ================================================================
    protected override void OnCreate()
    {
        base.OnCreate();

        Exiled.API.Features.Log.Debug(
            $"[WarheadBoomEffect] Spawning schematic at pos={_position}, rot={_rotation}, scale={_scale}");

        _schematicObject = ObjectSpawner.SpawnSchematic("WarheadBoomEffect", _position, _rotation);

        if (_schematicObject == null)
        {
            Exiled.API.Features.Log.Error(
                "[WarheadBoomEffect] SpawnSchematic returned null! Schematic名またはファイル配置を確認してください。");
            return;
        }

        // Scale をスポーン後に適用
        _schematicObject.Scale = _scale;

        _spawnPosition = _position;
        _initialScale  = _scale;
        _isAnimating   = false;
    }

    protected override void OnDestroy()
    {
        StopSmoke();
        _schematicObject?.Destroy();
        _schematicObject = null;
        base.OnDestroy();
    }

    // ================================================================
    //  Public API
    // ================================================================

    /// <summary>煙アニメーションを開始する。</summary>
    public void StartSmoke()
    {
        if (_isAnimating || _schematicObject == null) return;
        _isAnimating    = true;
        _smokeCoroutine = Timing.RunCoroutine(SmokeRiseRoutine());
    }

    /// <summary>煙アニメーションを途中停止する。</summary>
    public void StopSmoke()
    {
        if (!_isAnimating) return;
        _isAnimating = false;
        if (_smokeCoroutine.IsRunning)
            Timing.KillCoroutines(_smokeCoroutine);
    }

    // ================================================================
    //  煙上昇コルーチン
    // ================================================================
    private IEnumerator<float> SmokeRiseRoutine()
    {
        if (_schematicObject == null) yield break;

        Vector3 currentPos   = _spawnPosition;
        Vector3 currentScale = _initialScale;

        Vector3 drift      = RandomDrift();
        float   driftTimer = RandomDriftInterval();

        float riseSpeed = (float)(_rng.NextDouble()
                          * (SmokeConfig.RiseSpeedMax - SmokeConfig.RiseSpeedMin)
                          + SmokeConfig.RiseSpeedMin);

        float elapsed       = 0f;
        float totalDuration = SmokeConfig.MaxRiseHeight / riseSpeed;

        while (elapsed < totalDuration && _schematicObject != null)
        {
            float dt = SmokeConfig.TickInterval;

            // ドリフト方向を定期的にランダム変化
            driftTimer -= dt;
            if (driftTimer <= 0f)
            {
                drift      = RandomDrift();
                driftTimer = RandomDriftInterval();
            }

            // 高度比 (0→1)
            float heightRatio = Mathf.Clamp01(elapsed / totalDuration);

            // 上昇量 (高くなるほど減衰)
            float verticalDelta = riseSpeed * dt * (1f - heightRatio * 0.4f);

            currentPos += new Vector3(drift.x * dt, verticalDelta, drift.z * dt);
            _schematicObject.Position = currentPos;

            // スケール膨張
            float scaleMultiplier = Mathf.Lerp(1f, SmokeConfig.ScaleMultiplierAtMax, heightRatio);
            _schematicObject.Scale = currentScale * scaleMultiplier;

            elapsed += dt;
            yield return Timing.WaitForSeconds(SmokeConfig.TickInterval);
        }

        _isAnimating = false;
        Destroy();
    }

    // ================================================================
    //  ヘルパー
    // ================================================================
    private Vector3 RandomDrift() => new(
        (float)(_rng.NextDouble() * (SmokeConfig.DriftMax - SmokeConfig.DriftMin) + SmokeConfig.DriftMin),
        0f,
        (float)(_rng.NextDouble() * (SmokeConfig.DriftMax - SmokeConfig.DriftMin) + SmokeConfig.DriftMin)
    );

    private float RandomDriftInterval() =>
        (float)(_rng.NextDouble()
                * (SmokeConfig.DriftChangeIntervalMax - SmokeConfig.DriftChangeIntervalMin)
                + SmokeConfig.DriftChangeIntervalMin);
}