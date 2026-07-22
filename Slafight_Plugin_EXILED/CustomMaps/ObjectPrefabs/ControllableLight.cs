using System.Collections.Generic;
using Exiled.API.Features;
using MEC;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;
using LightSourceToy = LabApi.Features.Wrappers.LightSourceToy;

#pragma warning disable CS0618 // 元 LightSourceToy の旧 LightShape も欠落なく複製する。

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

/// <summary>
/// IsOn と Level(0-100) に応じて、<see cref="LightColor"/> と黒の間を自然にグラデーションさせる
/// 調光可能な ObjectPrefab。UnSpawn はせず、常に LightSourceToy の Color を変更するだけで
/// 点灯・消灯を表現する。
/// </summary>
public sealed class ControllableLight : ObjectPrefab
{
    private const float MinTransitionDuration = 0.02f;

    private LightSourceToy? _light;
    private CoroutineHandle _transitionHandle;
    private Color _currentColor = Color.black;
    private bool _isOn = true;
    private int _level = 100;

    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (_isOn == value)
                return;

            _isOn = value;
            BeginTransition();
        }
    }

    /// <summary>点灯度合い(0-100)。100 で <see cref="LightColor"/> そのもの、0 に近づくほど黒に寄る。</summary>
    public int Level
    {
        get => _level;
        set
        {
            int clamped = Mathf.Clamp(value, 0, 100);
            if (_level == clamped)
                return;

            _level = clamped;
            BeginTransition();
        }
    }

    public Color LightColor { get; set; } = Color.white;
    public float TransitionDuration { get; set; } = 1f;
    public float Intensity { get; set; } = 1f;
    public float Range { get; set; } = 10f;
    public LightShadows ShadowType { get; set; } = LightShadows.None;
    public float ShadowStrength { get; set; } = 1f;
    public LightType LightType { get; set; } = LightType.Point;
    public LightShape LightShape { get; set; } = LightShape.Cone;
    public float SpotAngle { get; set; } = 50f;
    public float InnerSpotAngle { get; set; } = 40f;

    private Color TargetColor => IsOn ? Color.Lerp(Color.black, LightColor, Level / 100f) : Color.black;

    protected override void OnCreate()
    {
        _currentColor = TargetColor;

        _light = LightSourceToy.Create(Position, Rotation, Scale, networkSpawn: false);
        _light.Intensity = Intensity;
        _light.Range = Range;
        _light.Color = _currentColor;
        _light.ShadowType = ShadowType;
        _light.ShadowStrength = ShadowStrength;
        _light.Type = LightType;
        _light.Shape = LightShape;
        _light.SpotAngle = SpotAngle;
        _light.InnerSpotAngle = InnerSpotAngle;
        _light.Spawn();
    }

    protected override void OnTransformUpdated()
    {
        base.OnTransformUpdated();

        if (_light == null)
            return;

        _light.Position = Position;
        _light.Rotation = Rotation;
        _light.Scale = Scale;
    }

    protected override void OnDestroy()
    {
        Timing.KillCoroutines(_transitionHandle);

        if (_light == null)
            return;

        _light.Destroy();
        _light = null;
    }

    private void BeginTransition()
    {
        // OnCreate 前(Option 適用タイミング等)はライト未生成のため、次の OnCreate 側で反映される。
        if (_light == null)
            return;

        Timing.KillCoroutines(_transitionHandle);
        _transitionHandle = Timing.RunCoroutine(TransitionCoroutine(TargetColor));
    }

    private IEnumerator<float> TransitionCoroutine(Color target)
    {
        Color start = _currentColor;
        float duration = Mathf.Max(MinTransitionDuration, TransitionDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Timing.DeltaTime;
            _currentColor = Color.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            if (_light != null)
                _light.Color = _currentColor;

            yield return Timing.WaitForOneFrame;
        }

        _currentColor = target;
        if (_light != null)
            _light.Color = _currentColor;
    }
}
