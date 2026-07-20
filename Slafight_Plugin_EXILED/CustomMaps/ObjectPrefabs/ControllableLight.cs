using System.Collections.Generic;
using System.Linq;
using AdminToys;
using Exiled.API.Features;
using LabApi.Features.Wrappers;
using MEC;
using Mirror;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;
using LightSourceToy = LabApi.Features.Wrappers.LightSourceToy;

#pragma warning disable CS0618 // 元 LightSourceToy の旧 LightShape も欠落なく複製する。

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

/// <summary>
/// 元の LightSourceToy を再現し、弾頭作動中だけ警報灯として動作させる ObjectPrefab。
/// </summary>
public sealed class ControllableLight : ObjectPrefab
{
    private static readonly List<ControllableLight> Instances = [];
    private static readonly Color AlarmRed = new(1f, 0f, 0f);
    private static readonly Color AlarmBrightRed = new(1f, 0.1f, 0.2f);
    private static readonly Color OmegaBlue = new(0f, 0f, 1f);
    private static readonly Color OmegaBrightBlue = new(0.1f, 0.2f, 1f);

    private const float AlarmTickInterval = 0.1f;
    private const float AlarmPulseSpeed = 8f;
    private const float AlarmRotationSpeed = 120f;
    private const int ReplacementsPerFrame = 2;

    private static CoroutineHandle _replacementHandle;
    private static CoroutineHandle _alarmHandle;
    private static float _alarmElapsed;

    private LightSourceToy? _mainLight;
    private LightSourceToy? _leftSpotLight;
    private LightSourceToy? _rightSpotLight;
    private AlarmMode _alarmMode;

    private enum AlarmMode
    {
        None,
        Alpha,
        Omega,
    }

    public override bool IsSaveable { get; set; } = false;
    public float Intensity { get; set; } = 1f;
    public float Range { get; set; } = 10f;
    public Color NormalColor { get; set; } = Color.white;
    public LightShadows ShadowType { get; set; } = LightShadows.None;
    public float ShadowStrength { get; set; } = 1f;
    public LightType LightType { get; set; } = LightType.Point;
    public LightShape LightShape { get; set; } = LightShape.Cone;
    public float SpotAngle { get; set; } = 50f;
    public float InnerSpotAngle { get; set; } = 40f;

    public static void ReplaceAll()
    {
        // 最初に対象を固定する。以降に生成された LightSourceToy は今回の置換対象に含めない。
        LightSourceToy[] originals = UnityEngine.Object
            .FindObjectsByType<AdminToys.LightSourceToy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(light => light != null && light.netIdentity != null && light.netIdentity.isServer)
            .Select(LightSourceToy.Get)
            .Where(light => light != null)
            .ToArray()!;

        Timing.KillCoroutines(_replacementHandle);
        _replacementHandle = Timing.RunCoroutine(ReplaceCoroutine(originals));
    }

    public static void CancelReplacement()
    {
        Timing.KillCoroutines(_replacementHandle);
    }

    private static IEnumerator<float> ReplaceCoroutine(LightSourceToy[] originals)
    {
        int replaced = 0;
        for (int i = 0; i < originals.Length; i++)
        {
            LightSourceToy original = originals[i];
            try
            {
                var controllable = new ControllableLight
                {
                    Position = original.Position,
                    Rotation = original.Rotation,
                    Scale = original.Scale,
                    Intensity = original.Intensity,
                    Range = original.Range,
                    NormalColor = original.Color,
                    ShadowType = original.ShadowType,
                    ShadowStrength = original.ShadowStrength,
                    LightType = original.Type,
                    LightShape = original.Shape,
                    SpotAngle = original.SpotAngle,
                    InnerSpotAngle = original.InnerSpotAngle,
                };

                controllable.Create();
                NetworkServer.UnSpawn(original.GameObject);
                replaced++;
            }
            catch (System.Exception ex)
            {
                Log.Error($"[ControllableLight] LightSourceToy の置換に失敗しました: {ex}");
            }

            if ((i + 1) % ReplacementsPerFrame == 0)
                yield return Timing.WaitForOneFrame;
        }

        Log.Info($"[ControllableLight] {replaced}/{originals.Length} 個の LightSourceToy を置換しました。");
    }

    public static void SetAlarmForAll(bool active)
    {
        _alarmElapsed = 0f;
        foreach (ControllableLight light in Instances.ToArray())
            light.SetAlarm(active ? AlarmMode.Alpha : AlarmMode.None);
        EnsureAlarmCoroutine();
    }

    public static void SetOmegaAlarmForAll(bool active)
    {
        _alarmElapsed = 0f;
        foreach (ControllableLight light in Instances.ToArray())
            light.SetAlarm(active ? AlarmMode.Omega : AlarmMode.None);
        EnsureAlarmCoroutine();
    }

    protected override void OnCreate()
    {
        _mainLight = CreateLight(LightType, NormalColor, Intensity, Rotation);
        _leftSpotLight = CreateLight(LightType.Spot, AlarmRed, 0f, Rotation * Quaternion.Euler(0f, 90f, 0f));
        _rightSpotLight = CreateLight(LightType.Spot, AlarmRed, 0f, Rotation * Quaternion.Euler(0f, -90f, 0f));
        Instances.Add(this);

        if (Features.OmegaWarhead.IsWarheadStarted)
            SetAlarm(AlarmMode.Omega);
        else if (Exiled.API.Features.Warhead.IsInProgress)
            SetAlarm(AlarmMode.Alpha);

        EnsureAlarmCoroutine();
    }

    protected override void OnTransformUpdated()
    {
        base.OnTransformUpdated();
        ApplyTransform(_mainLight, Rotation);

        if (_alarmMode == AlarmMode.None)
        {
            ApplyTransform(_leftSpotLight, Rotation * Quaternion.Euler(0f, 90f, 0f));
            ApplyTransform(_rightSpotLight, Rotation * Quaternion.Euler(0f, -90f, 0f));
        }
    }

    protected override void OnDestroy()
    {
        SetAlarm(AlarmMode.None);
        Instances.Remove(this);
        DestroyLight(ref _mainLight);
        DestroyLight(ref _leftSpotLight);
        DestroyLight(ref _rightSpotLight);
    }

    private LightSourceToy CreateLight(LightType type, Color color, float intensity, Quaternion rotation)
    {
        LightSourceToy light = LightSourceToy.Create(Position, rotation, Scale, networkSpawn: false);
        light.Intensity = intensity;
        light.Range = Range;
        light.Color = color;
        light.ShadowType = ShadowType;
        light.ShadowStrength = ShadowStrength;
        light.Type = type;
        light.Shape = LightShape;
        light.SpotAngle = SpotAngle;
        light.InnerSpotAngle = InnerSpotAngle;
        light.Spawn();
        return light;
    }

    private void SetAlarm(AlarmMode mode)
    {
        if (_alarmMode == mode)
            return;

        _alarmMode = mode;

        if (mode == AlarmMode.None)
        {
            if (_mainLight != null)
                _mainLight.Color = NormalColor;
            if (_leftSpotLight != null)
                _leftSpotLight.Intensity = 0f;
            if (_rightSpotLight != null)
                _rightSpotLight.Intensity = 0f;
            return;
        }

        Color initialColor = mode == AlarmMode.Omega ? OmegaBlue : AlarmRed;
        if (_mainLight != null)
            _mainLight.Color = initialColor;
        if (_leftSpotLight != null)
        {
            _leftSpotLight.Color = initialColor;
            _leftSpotLight.Intensity = Intensity;
        }
        if (_rightSpotLight != null)
        {
            _rightSpotLight.Color = initialColor;
            _rightSpotLight.Intensity = Intensity;
        }
    }

    private static void EnsureAlarmCoroutine()
    {
        if (!_alarmHandle.IsRunning && Instances.Any(light => light._alarmMode != AlarmMode.None))
            _alarmHandle = Timing.RunCoroutine(AlarmCoroutine());
    }

    private static IEnumerator<float> AlarmCoroutine()
    {
        while (Instances.Any(light => light._alarmMode != AlarmMode.None))
        {
            _alarmElapsed += AlarmTickInterval;
            foreach (ControllableLight light in Instances.ToArray())
                light.UpdateAlarm(_alarmElapsed);

            yield return Timing.WaitForSeconds(AlarmTickInterval);
        }
    }

    private void UpdateAlarm(float elapsed)
    {
        if (_alarmMode == AlarmMode.None)
            return;

        float pulse = (Mathf.Sin(elapsed * AlarmPulseSpeed) + 1f) * 0.5f;
        Color alarmColor = _alarmMode == AlarmMode.Omega
            ? Color.Lerp(OmegaBlue, OmegaBrightBlue, pulse)
            : Color.Lerp(AlarmRed, AlarmBrightRed, pulse);

        if (_mainLight != null)
            _mainLight.Color = alarmColor;
        if (_leftSpotLight != null)
        {
            _leftSpotLight.Color = alarmColor;
            _leftSpotLight.Rotation = Rotation * Quaternion.Euler(0f, 90f + elapsed * AlarmRotationSpeed, 0f);
        }
        if (_rightSpotLight != null)
        {
            _rightSpotLight.Color = alarmColor;
            _rightSpotLight.Rotation = Rotation * Quaternion.Euler(0f, -90f - elapsed * AlarmRotationSpeed, 0f);
        }
    }

    private void ApplyTransform(LightSourceToy? light, Quaternion rotation)
    {
        if (light == null)
            return;

        light.Position = Position;
        light.Rotation = rotation;
        light.Scale = Scale;
    }

    private static void DestroyLight(ref LightSourceToy? light)
    {
        if (light == null)
            return;

        light.Destroy();
        light = null;
    }
}
