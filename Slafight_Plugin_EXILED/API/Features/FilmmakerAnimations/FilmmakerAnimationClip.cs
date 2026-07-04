using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using PlayerRoles.Filmmaker;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Features.FilmmakerAnimations;

public sealed class FilmmakerAnimationClip
{
    public const int CurrentVersion = 1;
    public const float OriginalFrameRate = FilmmakerTimelineManager.FrameRate;

    private bool _normalized;

    public int Version { get; set; } = CurrentVersion;
    public string Name { get; set; } = string.Empty;
    public string Source { get; set; } = "PlayerRoles.Filmmaker.FilmmakerTimelineManager";
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public float FrameRate { get; set; } = OriginalFrameRate;
    public List<FilmmakerVector3Keyframe> Positions { get; set; } = [];
    public List<FilmmakerQuaternionKeyframe> Rotations { get; set; } = [];
    public List<FilmmakerFloatKeyframe> Zooms { get; set; } = [];

    [JsonIgnore]
    public float EffectiveFrameRate => FrameRate > 0.001f ? FrameRate : OriginalFrameRate;

    [JsonIgnore]
    public bool HasPositionTrack => Positions is { Count: > 0 };

    [JsonIgnore]
    public bool HasRotationTrack => Rotations is { Count: > 0 };

    [JsonIgnore]
    public bool HasZoomTrack => Zooms is { Count: > 0 };

    [JsonIgnore]
    public bool HasPlayableMotion => HasPositionTrack || HasRotationTrack;

    [JsonIgnore]
    public int FirstFrame
    {
        get
        {
            int? min = null;
            ReadMinFrame(Positions, ref min);
            ReadMinFrame(Rotations, ref min);
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
            ReadMaxFrame(Zooms, ref max);
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
            Positions = FilmmakerTimelineManager.PositionTrack.Keyframes
                .Select(k => FilmmakerVector3Keyframe.From(k.TimeFrames, k.BlendCurve, k.Value))
                .ToList(),
            Rotations = FilmmakerTimelineManager.RotationTrack.Keyframes
                .Select(k => FilmmakerQuaternionKeyframe.From(k.TimeFrames, k.BlendCurve, k.Value))
                .ToList(),
            Zooms = FilmmakerTimelineManager.ZoomTrack.Keyframes
                .Select(k => FilmmakerFloatKeyframe.From(k.TimeFrames, k.BlendCurve, k.Value))
                .ToList(),
        };

        clip.Normalize();
        return clip;
    }

    public void Normalize()
    {
        Version = Version <= 0 ? CurrentVersion : Version;
        FrameRate = EffectiveFrameRate;
        Positions = (Positions ?? []).OrderBy(frame => frame.TimeFrames).ToList();
        Rotations = (Rotations ?? []).OrderBy(frame => frame.TimeFrames).ToList();
        Zooms = (Zooms ?? []).OrderBy(frame => frame.TimeFrames).ToList();
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
        if (!HasRotationTrack)
        {
            rotation = default;
            return false;
        }

        rotation = Rotations[0].Value;
        return true;
    }

    public bool TryEvaluate(float seconds, out FilmmakerAnimationSample sample)
    {
        EnsureNormalized();
        seconds = Mathf.Max(0f, seconds);
        int timeFrames = Mathf.RoundToInt(seconds * EffectiveFrameRate);

        bool hasPosition = TryEvaluateVector(Positions, timeFrames, seconds, EffectiveFrameRate, out Vector3 position);
        bool hasRotation = TryEvaluateQuaternion(Rotations, timeFrames, seconds, EffectiveFrameRate, out Quaternion rotation);
        bool hasZoom = TryEvaluateFloat(Zooms, timeFrames, seconds, EffectiveFrameRate, out float zoom);

        sample = new FilmmakerAnimationSample(position, rotation, zoom, hasPosition, hasRotation, hasZoom);
        return hasPosition || hasRotation || hasZoom;
    }

    private void EnsureNormalized()
    {
        if (!_normalized)
            Normalize();
    }

    private static bool TryEvaluateVector(
        IReadOnlyList<FilmmakerVector3Keyframe> keyframes,
        int timeFrames,
        float seconds,
        float frameRate,
        out Vector3 value)
    {
        if (!TryFindBounds(keyframes, timeFrames, out var previous, out var next))
        {
            value = default;
            return false;
        }

        value = Vector3.Lerp(previous.Value, next.Value, EvaluateBlend(previous, next, seconds, frameRate));
        return true;
    }

    private static bool TryEvaluateQuaternion(
        IReadOnlyList<FilmmakerQuaternionKeyframe> keyframes,
        int timeFrames,
        float seconds,
        float frameRate,
        out Quaternion value)
    {
        if (!TryFindBounds(keyframes, timeFrames, out var previous, out var next))
        {
            value = default;
            return false;
        }

        value = Quaternion.Lerp(previous.Value, next.Value, EvaluateBlend(previous, next, seconds, frameRate));
        return true;
    }

    private static bool TryEvaluateFloat(
        IReadOnlyList<FilmmakerFloatKeyframe> keyframes,
        int timeFrames,
        float seconds,
        float frameRate,
        out float value)
    {
        if (!TryFindBounds(keyframes, timeFrames, out var previous, out var next))
        {
            value = default;
            return false;
        }

        value = Mathf.Lerp(previous.Value, next.Value, EvaluateBlend(previous, next, seconds, frameRate));
        return true;
    }

    private static bool TryFindBounds<TFrame>(
        IReadOnlyList<TFrame> keyframes,
        int timeFrames,
        out TFrame previous,
        out TFrame next)
        where TFrame : FilmmakerAnimationKeyframe
    {
        previous = null;
        next = null;

        if (keyframes == null || keyframes.Count == 0)
            return false;

        for (int i = keyframes.Count - 1; i >= 0; i--)
        {
            if (keyframes[i].TimeFrames <= timeFrames)
            {
                previous = keyframes[i];
                break;
            }
        }

        for (int i = 0; i < keyframes.Count; i++)
        {
            if (keyframes[i].TimeFrames >= timeFrames)
            {
                next = keyframes[i];
                break;
            }
        }

        previous ??= next;
        next ??= previous;
        return previous != null && next != null;
    }

    private static float EvaluateBlend(
        FilmmakerAnimationKeyframe previous,
        FilmmakerAnimationKeyframe next,
        float seconds,
        float frameRate)
    {
        if (!FilmmakerTimelineManager.BlendPresets.TryGetValue(previous.Blend, out AnimationCurve curve))
            curve = FilmmakerTimelineManager.BlendPresets[FilmmakerBlendPreset.Linear];

        float previousSeconds = previous.TimeFrames / frameRate;
        float nextSeconds = next.TimeFrames / frameRate;
        return curve.Evaluate(Mathf.InverseLerp(previousSeconds, nextSeconds, seconds));
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
