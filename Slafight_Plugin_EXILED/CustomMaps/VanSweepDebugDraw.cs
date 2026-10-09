using System.Collections;
using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.API.Features.Toys;
using MEC;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps;

/// <summary>
/// 車両スイープの判定箱を Primitive で描くためのデバッグ表示です。
/// </summary>
/// <remarks>
/// <para>
/// 判定に使う箱は Collider ではないので <c>slc hitbox</c> のコライダ表示では見えません。
/// そこで同じ <see cref="RespawnVehiclePhysicsImpact.TryGetCurrentShape"/> を使って
/// 箱の枠だけを描くようにしました。描画と判定が同じ値なので、
/// 見た目で車体に合っているかそのまま判断できます。
/// </para>
/// <para>
/// <c>slc hitbox van</c> で表示の切り替え、<c>slc hitbox van size|offset</c> で
/// 箱の実寸と中心オフセットを手動で変えられます。
/// </para>
/// </remarks>
internal static class VanSweepDebugDraw
{
    private const float RedrawInterval = 0.1f;
    private const float LineWidth = 0.05f;
    private const int EdgesPerBox = 12;

    private static readonly List<Primitive> Lines = new();
    private static readonly List<(Vector3 start, Vector3 end)> Segments = new();
    private static CoroutineHandle _handle;

    /// <summary>判定箱を描いているかどうか。</summary>
    public static bool Enabled { get; private set; }

    public static void SetEnabled(bool enabled)
    {
        if (Enabled == enabled)
            return;

        Enabled = enabled;
        if (enabled)
        {
            if (!_handle.IsRunning)
                _handle = Timing.RunCoroutine(Loop());

            return;
        }

        Timing.KillCoroutines(_handle);
        _handle = default;
        ClearLines();
    }

    private static IEnumerator<float> Loop()
    {
        while (Enabled)
        {
            Redraw();
            yield return Timing.WaitForSeconds(RedrawInterval);
        }
    }

    private static void Redraw()
    {
        Segments.Clear();

        foreach (RespawnVehiclePhysicsImpact impact in RespawnVehiclePhysicsImpact.ActiveInstances)
        {
            if (impact == null)
                continue;

            if (!impact.TryGetCurrentShape(out Vector3 center, out Vector3 halfExtents, out Quaternion rotation))
                continue;

            AddBoxSegments(center, halfExtents, rotation);
        }

        EnsureLineCount(Segments.Count);

        for (int i = 0; i < Segments.Count; i++)
        {
            UpdateLine(Lines[i], Segments[i].start, Segments[i].end, Color.yellow);
        }

        for (int i = Segments.Count; i < Lines.Count; i++)
            Lines[i].Scale = Vector3.zero;
    }

    private static void AddBoxSegments(Vector3 center, Vector3 halfExtents, Quaternion rotation)
    {
        Vector3 right = rotation * Vector3.right * halfExtents.x;
        Vector3 up = rotation * Vector3.up * halfExtents.y;
        Vector3 forward = rotation * Vector3.forward * halfExtents.z;

        Vector3 p000 = center - right - up - forward;
        Vector3 p100 = center + right - up - forward;
        Vector3 p110 = center + right - up + forward;
        Vector3 p010 = center - right - up + forward;
        Vector3 p001 = center - right + up - forward;
        Vector3 p101 = center + right + up - forward;
        Vector3 p111 = center + right + up + forward;
        Vector3 p011 = center - right + up + forward;

        Segments.Add((p000, p100));
        Segments.Add((p100, p110));
        Segments.Add((p110, p010));
        Segments.Add((p010, p000));
        Segments.Add((p001, p101));
        Segments.Add((p101, p111));
        Segments.Add((p111, p011));
        Segments.Add((p011, p001));
        Segments.Add((p000, p001));
        Segments.Add((p100, p101));
        Segments.Add((p110, p111));
        Segments.Add((p010, p011));
    }

    private static void EnsureLineCount(int count)
    {
        while (Lines.Count < count)
        {
            Primitive primitive = Primitive.Create(
                PrimitiveType.Cube,
                Vector3.zero,
                Vector3.zero,
                Vector3.zero,
                true,
                Color.yellow);

            primitive.Collidable = false;
            Lines.Add(primitive);
        }
    }

    private static void UpdateLine(Primitive primitive, Vector3 start, Vector3 end, Color color)
    {
        Vector3 delta = end - start;
        float length = delta.magnitude;
        if (length <= 0.001f)
        {
            primitive.Scale = Vector3.zero;
            return;
        }

        primitive.Position = Vector3.Lerp(start, end, 0.5f);
        primitive.Rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
        primitive.Scale = new Vector3(LineWidth, LineWidth, length);
        primitive.Color = color;
        primitive.Collidable = false;
    }

    private static void ClearLines()
    {
        foreach (Primitive primitive in Lines)
        {
            if (primitive == null)
                continue;

            try
            {
                primitive.RemoveShowState();
                primitive.Destroy();
            }
            catch (System.Exception exception)
            {
                Log.Debug($"[VanSweepDebugDraw] 描画の破棄に失敗しました: {exception.Message}");
            }
        }

        Lines.Clear();
    }
}
