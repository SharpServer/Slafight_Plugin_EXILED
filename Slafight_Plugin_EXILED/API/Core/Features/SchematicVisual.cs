using AdminToys;
using ProjectMER.Features.Objects;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// Schematic の見た目 (プリミティブの不透明度とライトの明るさ) を、生成時の値に対する割合でまとめて操作します。
/// </summary>
/// <remarks>
/// 値が変わらない要素には書き込まないので、毎フレーム呼んでも同期は増えません。
/// 動かしたり薄めたりする Schematic は最適化を切って生成してください (<see cref="SchematicSpawner.TrySpawnMovable"/>)。
/// </remarks>
public sealed class SchematicVisual
{
    // 1/255 未満の差は見た目に出ないので書かない。
    private const float Epsilon = 1f / 255f;

    private readonly PrimitiveObjectToy[] primitives;
    private readonly Color[] baseColors;
    private readonly LightSourceToy[] lights;
    private readonly float[] baseIntensities;

    private float opacity = 1f;
    private float lightScale = 1f;

    /// <summary>今の状態を基準として記録します。</summary>
    public SchematicVisual(SchematicObject schematic)
    {
        primitives = schematic != null ? schematic.GetComponentsInChildren<PrimitiveObjectToy>(true) : [];
        lights = schematic != null ? schematic.GetComponentsInChildren<LightSourceToy>(true) : [];

        baseColors = new Color[primitives.Length];
        for (int i = 0; i < primitives.Length; i++)
            baseColors[i] = primitives[i].NetworkMaterialColor;

        baseIntensities = new float[lights.Length];
        for (int i = 0; i < lights.Length; i++)
            baseIntensities[i] = lights[i].NetworkLightIntensity;
    }

    /// <summary>プリミティブの不透明度の倍率です。0 で透明、1 で生成時のまま。</summary>
    public float Opacity
    {
        get => opacity;
        set
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Abs(value - opacity) < Epsilon) return;

            opacity = value;
            for (int i = 0; i < primitives.Length; i++)
            {
                if (primitives[i] == null) continue;

                Color color = baseColors[i];
                color.a *= value;
                primitives[i].NetworkMaterialColor = color;
            }
        }
    }

    /// <summary>ライトの明るさの倍率です。0 で消灯、1 で生成時のまま。</summary>
    public float LightScale
    {
        get => lightScale;
        set
        {
            value = Mathf.Max(0f, value);
            if (Mathf.Abs(value - lightScale) < Epsilon) return;

            lightScale = value;
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null)
                    lights[i].NetworkLightIntensity = baseIntensities[i] * value;
            }
        }
    }

    /// <summary>不透明度とライトを同じ割合にします。0 で見えなくなります。</summary>
    public void Fade(float visibility)
    {
        Opacity = visibility;
        LightScale = Mathf.Clamp01(visibility);
    }

    /// <summary>生成時の見た目に戻します。</summary>
    public void Restore() => Fade(1f);
}
