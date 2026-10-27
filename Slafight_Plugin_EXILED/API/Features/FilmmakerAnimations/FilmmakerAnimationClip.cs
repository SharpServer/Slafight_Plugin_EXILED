using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using PlayerRoles;
using PlayerRoles.Filmmaker;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Features.FilmmakerAnimations;

/// <summary>
/// キーフレームの間の補間方法です。
/// </summary>
public enum FilmmakerInterpolation
{
    /// <summary>キーの間を直線で結びます。記録した密なクリップ向けです。</summary>
    Linear,

    /// <summary>キーを滑らかな曲線で通ります。手で置いた疎なキー向けです。</summary>
    Spline,
}

public sealed class FilmmakerAnimationClip
{
    public const int CurrentVersion = 2;
    public const float OriginalFrameRate = FilmmakerTimelineManager.FrameRate;

    /// <summary>
    /// <c>Player.Position</c> から目線までの高さ (Tutorial の概算) です。
    /// </summary>
    public const float DefaultEyeHeight = 0.7f;

    private bool _normalized;

    public int Version { get; set; } = CurrentVersion;
    public string Name { get; set; } = string.Empty;
    public string Source { get; set; } = "PlayerRoles.Filmmaker.FilmmakerTimelineManager";
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public float FrameRate { get; set; } = OriginalFrameRate;
    public FilmmakerInterpolation Interpolation { get; set; } = FilmmakerInterpolation.Linear;

    /// <summary>
    /// 注視点から向きを求めるときの目線の高さです。
    /// </summary>
    public float EyeHeight { get; set; } = DefaultEyeHeight;

    public List<FilmmakerVector3Keyframe> Positions { get; set; } = [];
    public List<FilmmakerQuaternionKeyframe> Rotations { get; set; } = [];

    /// <summary>
    /// 注視点です。あれば向きは <see cref="Rotations"/> ではなくここから求めます。
    /// </summary>
    public List<FilmmakerVector3Keyframe> LookTargets { get; set; } = [];

    public List<FilmmakerFloatKeyframe> Zooms { get; set; } = [];

    /// <summary>
    /// 再生中に <see cref="FilmmakerCutscene.CueReached"/> を起こす合図です。
    /// </summary>
    public List<FilmmakerCue> Cues { get; set; } = [];

    /// <summary>
    /// ダミーが役者として再生するときに実行するアクション (<see cref="DummyActions"/> のパス) です。
    /// </summary>
    public List<FilmmakerActionKey> Actions { get; set; } = [];

    /// <summary>
    /// 役者として出すときの既定の役職です。記録したときの役職が入ります。
    /// </summary>
    public RoleTypeId? ActorRole { get; set; }

    /// <summary>
    /// 役者に持たせるアイテムです。記録したときの所持品が入ります。
    /// </summary>
    public List<ItemType> ActorItems { get; set; } = [];

    [JsonIgnore]
    public float EffectiveFrameRate => FrameRate > 0.001f ? FrameRate : OriginalFrameRate;

    [JsonIgnore]
    public bool HasPositionTrack => Positions is { Count: > 0 };

    [JsonIgnore]
    public bool HasRotationTrack => Rotations is { Count: > 0 };

    [JsonIgnore]
    public bool HasLookTrack => LookTargets is { Count: > 0 };

    [JsonIgnore]
    public bool HasZoomTrack => Zooms is { Count: > 0 };

    [JsonIgnore]
    public bool HasPlayableMotion => HasPositionTrack || HasRotationTrack || HasLookTrack;

    [JsonIgnore]
    public int FirstFrame
    {
        get
        {
            int? min = null;
            ReadMinFrame(Positions, ref min);
            ReadMinFrame(Rotations, ref min);
            ReadMinFrame(LookTargets, ref min);
            ReadMinFrame(Zooms, ref min);
            return min ?? 0;
        }
    }

    [JsonIgnore]
    public int LastFrame
    {
        get
        {
            int max = 0;
            ReadMaxFrame(Positions, ref max);
            ReadMaxFrame(Rotations, ref max);
            ReadMaxFrame(LookTargets, ref max);
            ReadMaxFrame(Zooms, ref max);

            // 最後のキーより後ろの合図やアクションも鳴らせるように、長さに含める。
            foreach (FilmmakerCue cue in Cues ?? [])
                max = Math.Max(max, cue.TimeFrames);

            foreach (FilmmakerActionKey action in Actions ?? [])
                max = Math.Max(max, action.TimeFrames);

            return max;
        }
    }

    [JsonIgnore]
    public float DurationSeconds => LastFrame / EffectiveFrameRate;

    public static FilmmakerAnimationClip FromCurrentTimeline(string name)
    {
        var clip = new FilmmakerAnimationClip
        {
            Name = name,
            Positions =
            [
                .. FilmmakerTimelineManager.PositionTrack.Keyframes
                    .Select(k => FilmmakerVector3Keyframe.From(k.TimeFrames, k.BlendCurve, k.Value))
            ],
            Rotations =
            [
                .. FilmmakerTimelineManager.RotationTrack.Keyframes
                    .Select(k => FilmmakerQuaternionKeyframe.From(k.TimeFrames, k.BlendCurve, k.Value))
            ],
            Zooms =
            [
                .. FilmmakerTimelineManager.ZoomTrack.Keyframes
                    .Select(k => FilmmakerFloatKeyframe.From(k.TimeFrames, k.BlendCurve, k.Value))
            ],
        };

        clip.Normalize();
        return clip;
    }

    public void Normalize()
    {
        Version = Version <= 0 ? CurrentVersion : Version;
        FrameRate = EffectiveFrameRate;
        Positions = [.. (Positions ?? []).OrderBy(frame => frame.TimeFrames)];
        Rotations = [.. (Rotations ?? []).OrderBy(frame => frame.TimeFrames)];
        LookTargets = [.. (LookTargets ?? []).OrderBy(frame => frame.TimeFrames)];
        Zooms = [.. (Zooms ?? []).OrderBy(frame => frame.TimeFrames)];
        Cues = [.. (Cues ?? []).Where(cue => !string.IsNullOrEmpty(cue.Name)).OrderBy(cue => cue.TimeFrames)];
        Actions = [.. (Actions ?? []).Where(action => !string.IsNullOrEmpty(action.Path)).OrderBy(action => action.TimeFrames)];
        ActorItems ??= [];
        _normalized = true;
    }

    public bool TryGetFirstPosition(out Vector3 position)
    {
        EnsureNormalized();
        if (!HasPositionTrack)
        {
            position = default;
            return false;
        }

        position = Positions[0].Value;
        return true;
    }

    public bool TryGetFirstRotation(out Quaternion rotation)
    {
        EnsureNormalized();
        if (TryEvaluate(FirstFrame / EffectiveFrameRate, out FilmmakerAnimationSample sample) && sample.HasRotation)
        {
            rotation = sample.Rotation;
            return true;
        }

        rotation = default;
        return false;
    }

    public bool TryEvaluate(float seconds, out FilmmakerAnimationSample sample)
    {
        EnsureNormalized();
        seconds = Mathf.Max(0f, seconds);
        bool spline = Interpolation == FilmmakerInterpolation.Spline;

        bool hasPosition = TryEvaluateVector(Positions, seconds, spline, out Vector3 position);
        bool hasRotation;
        Quaternion rotation;

        if (HasLookTrack && TryEvaluateVector(LookTargets, seconds, spline, out Vector3 target))
        {
            Vector3 eye = (hasPosition ? position : Vector3.zero) + Vector3.up * EyeHeight;
            Vector3 direction = target - eye;
            hasRotation = direction.sqrMagnitude > 0.0001f;
            rotation = hasRotation ? Quaternion.LookRotation(direction) : default;
        }
        else
        {
            hasRotation = TryEvaluateQuaternion(Rotations, seconds, spline, out rotation);
        }

        bool hasZoom = TryEvaluateFloat(Zooms, seconds, out float zoom);

        sample = new FilmmakerAnimationSample(position, rotation, zoom, hasPosition, hasRotation, hasZoom);
        return hasPosition || hasRotation || hasZoom;
    }

    private void EnsureNormalized()
    {
        if (!_normalized)
            Normalize();
    }

    private bool TryEvaluateVector(
        IReadOnlyList<FilmmakerVector3Keyframe> keyframes,
        float seconds,
        bool spline,
        out Vector3 value)
    {
        if (!TryFindSegment(keyframes, seconds, out int index, out float s))
        {
            value = default;
            return false;
        }

        if (s <= 0f || index + 1 >= keyframes.Count)
        {
            value = keyframes[index].Value;
            return true;
        }

        FilmmakerVector3Keyframe from = keyframes[index];
        FilmmakerVector3Keyframe to = keyframes[index + 1];

        if (!spline)
        {
            value = Vector3.LerpUnclamped(from.Value, to.Value, s);
            return true;
        }

        // 時間間隔が不揃いでも速度が繋がるように、秒あたりの接線で Hermite 補間する。
        float h = (to.TimeFrames - from.TimeFrames) / EffectiveFrameRate;
        Vector3 m0 = Tangent(keyframes, index);
        Vector3 m1 = Tangent(keyframes, index + 1);
        value = Hermite(from.Value, m0 * h, to.Value, m1 * h, s);
        return true;
    }

    private bool TryEvaluateQuaternion(
        IReadOnlyList<FilmmakerQuaternionKeyframe> keyframes,
        float seconds,
        bool spline,
        out Quaternion value)
    {
        if (!TryFindSegment(keyframes, seconds, out int index, out float s))
        {
            value = default;
            return false;
        }

        if (s <= 0f || index + 1 >= keyframes.Count)
        {
            value = keyframes[index].Value;
            return true;
        }

        Quaternion q1 = keyframes[index].Value;
        Quaternion q2 = Align(q1, keyframes[index + 1].Value);

        if (!spline)
        {
            value = Quaternion.Slerp(q1, q2, s);
            return true;
        }

        Quaternion q0 = HasIncoming(keyframes, index) ? Align(q1, keyframes[index - 1].Value) : q1;
        Quaternion q3 = HasOutgoing(keyframes, index + 1) ? Align(q2, keyframes[index + 2].Value) : q2;
        value = Squad(q1, q2, SquadControl(q0, q1, q2), SquadControl(q1, q2, q3), s);
        return true;
    }

    private bool TryEvaluateFloat(IReadOnlyList<FilmmakerFloatKeyframe> keyframes, float seconds, out float value)
    {
        if (!TryFindSegment(keyframes, seconds, out int index, out float s))
        {
            value = default;
            return false;
        }

        value = s <= 0f || index + 1 >= keyframes.Count
            ? keyframes[index].Value
            : Mathf.LerpUnclamped(keyframes[index].Value, keyframes[index + 1].Value, s);
        return true;
    }

    /// <summary>
    /// <paramref name="seconds"/> を含む区間の始点と、補間方法を反映した区間内の進み (0〜1) を求めます。
    /// </summary>
    private bool TryFindSegment<TFrame>(IReadOnlyList<TFrame> keyframes, float seconds, out int index, out float s)
        where TFrame : FilmmakerAnimationKeyframe
    {
        index = 0;
        s = 0f;

        if (keyframes == null || keyframes.Count == 0)
            return false;

        float frame = seconds * EffectiveFrameRate;
        if (frame <= keyframes[0].TimeFrames)
            return true;

        // 記録したクリップはキーフレームが多いので二分探索する。並びは Normalize 済み。
        int low = 0;
        int high = keyframes.Count - 1;
        while (low <= high)
        {
            int middle = (low + high) / 2;
            if (keyframes[middle].TimeFrames <= frame)
            {
                index = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        if (index + 1 >= keyframes.Count)
            return true;

        TFrame from = keyframes[index];
        TFrame to = keyframes[index + 1];
        float raw = Mathf.InverseLerp(from.TimeFrames, to.TimeFrames, frame);

        if (!FilmmakerTimelineManager.BlendPresets.TryGetValue(from.Blend, out AnimationCurve curve))
            curve = FilmmakerTimelineManager.BlendPresets[FilmmakerBlendPreset.Linear];

        s = curve.Evaluate(raw);
        return true;
    }

    /// <summary>
    /// キーでの秒あたりの接線です。前後どちらかが切れている (端・Hold) キーでは止まるよう 0 にします。
    /// </summary>
    private Vector3 Tangent(IReadOnlyList<FilmmakerVector3Keyframe> keyframes, int index)
    {
        if (!HasIncoming(keyframes, index) || !HasOutgoing(keyframes, index))
            return Vector3.zero;

        FilmmakerVector3Keyframe previous = keyframes[index - 1];
        FilmmakerVector3Keyframe next = keyframes[index + 1];
        float span = (next.TimeFrames - previous.TimeFrames) / EffectiveFrameRate;
        return span > 0.0001f ? (next.Value - previous.Value) / span : Vector3.zero;
    }

    private static bool HasIncoming<TFrame>(IReadOnlyList<TFrame> keyframes, int index)
        where TFrame : FilmmakerAnimationKeyframe
        => index > 0 && IsContinuous(keyframes[index - 1].Blend);

    private static bool HasOutgoing<TFrame>(IReadOnlyList<TFrame> keyframes, int index)
        where TFrame : FilmmakerAnimationKeyframe
        => index + 1 < keyframes.Count && IsContinuous(keyframes[index].Blend);

    private static bool IsContinuous(FilmmakerBlendPreset blend)
        => blend is FilmmakerBlendPreset.Linear or FilmmakerBlendPreset.Smooth;

    private static Vector3 Hermite(Vector3 p0, Vector3 m0, Vector3 p1, Vector3 m1, float s)
    {
        float s2 = s * s;
        float s3 = s2 * s;
        return (2f * s3 - 3f * s2 + 1f) * p0 +
               (s3 - 2f * s2 + s) * m0 +
               (-2f * s3 + 3f * s2) * p1 +
               (s3 - s2) * m1;
    }

    private static Quaternion Align(Quaternion reference, Quaternion value)
        => Quaternion.Dot(reference, value) < 0f ? new Quaternion(-value.x, -value.y, -value.z, -value.w) : value;

    private static Quaternion Squad(Quaternion q1, Quaternion q2, Quaternion a1, Quaternion a2, float s)
        => Quaternion.Slerp(Quaternion.Slerp(q1, q2, s), Quaternion.Slerp(a1, a2, s), 2f * s * (1f - s));

    private static Quaternion SquadControl(Quaternion previous, Quaternion current, Quaternion next)
    {
        Quaternion inverse = Quaternion.Inverse(current);
        Vector4 a = Log(inverse * next);
        Vector4 b = Log(inverse * previous);
        return current * Exp((a + b) * -0.25f);
    }

    private static Vector4 Log(Quaternion q)
    {
        float w = Mathf.Clamp(q.w, -1f, 1f);
        float angle = Mathf.Acos(w);
        float sin = Mathf.Sin(angle);
        if (Mathf.Abs(sin) < 0.0001f)
            return Vector4.zero;

        float scale = angle / sin;
        return new Vector4(q.x * scale, q.y * scale, q.z * scale, 0f);
    }

    private static Quaternion Exp(Vector4 v)
    {
        float angle = Mathf.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
        if (angle < 0.0001f)
            return Quaternion.identity;

        float scale = Mathf.Sin(angle) / angle;
        return new Quaternion(v.x * scale, v.y * scale, v.z * scale, Mathf.Cos(angle));
    }

    private static void ReadMinFrame<TFrame>(IEnumerable<TFrame> frames, ref int? min)
        where TFrame : FilmmakerAnimationKeyframe
    {
        if (frames == null)
            return;

        foreach (TFrame frame in frames)
        {
            min = min.HasValue ? Math.Min(min.Value, frame.TimeFrames) : frame.TimeFrames;
        }
    }

    private static void ReadMaxFrame<TFrame>(IEnumerable<TFrame> frames, ref int max)
        where TFrame : FilmmakerAnimationKeyframe
    {
        if (frames == null)
            return;

        foreach (TFrame frame in frames)
            max = Math.Max(max, frame.TimeFrames);
    }
}

public readonly struct FilmmakerAnimationSample
{
    public FilmmakerAnimationSample(
        Vector3 position,
        Quaternion rotation,
        float zoom,
        bool hasPosition,
        bool hasRotation,
        bool hasZoom)
    {
        Position = position;
        Rotation = rotation;
        Zoom = zoom;
        HasPosition = hasPosition;
        HasRotation = hasRotation;
        HasZoom = hasZoom;
    }

    public Vector3 Position { get; }
    public Quaternion Rotation { get; }
    public float Zoom { get; }
    public bool HasPosition { get; }
    public bool HasRotation { get; }
    public bool HasZoom { get; }
}

public abstract class FilmmakerAnimationKeyframe
{
    public int TimeFrames { get; set; }
    public FilmmakerBlendPreset Blend { get; set; } = FilmmakerBlendPreset.Linear;
}

public sealed class FilmmakerVector3Keyframe : FilmmakerAnimationKeyframe
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    [JsonIgnore]
    public Vector3 Value
    {
        get => new(X, Y, Z);
        set
        {
            X = value.x;
            Y = value.y;
            Z = value.z;
        }
    }

    public static FilmmakerVector3Keyframe From(int timeFrames, FilmmakerBlendPreset blend, Vector3 value)
        => new() { TimeFrames = timeFrames, Blend = blend, Value = value };
}

public sealed class FilmmakerQuaternionKeyframe : FilmmakerAnimationKeyframe
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float W { get; set; }

    [JsonIgnore]
    public Quaternion Value
    {
        get => new(X, Y, Z, W);
        set
        {
            X = value.x;
            Y = value.y;
            Z = value.z;
            W = value.w;
        }
    }

    public static FilmmakerQuaternionKeyframe From(int timeFrames, FilmmakerBlendPreset blend, Quaternion value)
        => new() { TimeFrames = timeFrames, Blend = blend, Value = value };
}

public sealed class FilmmakerFloatKeyframe : FilmmakerAnimationKeyframe
{
    public float Value { get; set; }

    public static FilmmakerFloatKeyframe From(int timeFrames, FilmmakerBlendPreset blend, float value)
        => new() { TimeFrames = timeFrames, Blend = blend, Value = value };
}

public sealed class FilmmakerCue
{
    public int TimeFrames { get; set; }
    public string Name { get; set; } = string.Empty;

    public static FilmmakerCue From(int timeFrames, string name)
        => new() { TimeFrames = timeFrames, Name = name };
}

public sealed class FilmmakerActionKey
{
    public int TimeFrames { get; set; }

    /// <summary><c>Module/Name</c> 形式のダミーアクションです。<c>*</c> を使えます。</summary>
    public string Path { get; set; } = string.Empty;

    public static FilmmakerActionKey From(int timeFrames, string path)
        => new() { TimeFrames = timeFrames, Path = path };
}
