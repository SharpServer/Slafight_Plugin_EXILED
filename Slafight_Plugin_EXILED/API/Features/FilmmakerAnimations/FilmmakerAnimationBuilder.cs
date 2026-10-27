using System;
using System.Collections.Generic;
using System.Linq;
using PlayerRoles;
using PlayerRoles.Filmmaker;
using PlayerRoles.FirstPersonControl;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Features.FilmmakerAnimations;

/// <summary>
/// コードからカメラワークを組み立てて <see cref="FilmmakerAnimationClip"/> を作ります。
/// </summary>
/// <remarks>
/// カメラは「位置」と「注視点」の 2 本で持ち、既定ではキーの間を曲線で滑らかに通ります。
/// 位置は <c>Player.Position</c> 基準で、目線は <see cref="EyeHeight"/> だけ上です。
/// 補間方法 (<see cref="FilmmakerBlendPreset"/>) はそのキーから次のキーまでの区間に掛かります。
/// </remarks>
public sealed class FilmmakerAnimationBuilder
{
    public const float DefaultPathInterval = 0.1f;

    /// <summary>
    /// 向きだけ指定されたときに置く注視点までの距離です。
    /// </summary>
    private const float LookDistance = 10f;

    private const float FrameRate = FilmmakerTimelineManager.FrameRate;

    private readonly string _name;
    private readonly List<FilmmakerVector3Keyframe> _positions = [];
    private readonly List<FilmmakerVector3Keyframe> _looks = [];
    private readonly List<FilmmakerFloatKeyframe> _zooms = [];
    private readonly List<FilmmakerCue> _cues = [];
    private readonly List<FilmmakerActionKey> _actions = [];

    public FilmmakerAnimationBuilder(string name)
    {
        _name = name ?? string.Empty;
    }

    /// <summary>
    /// 最後のキーの時刻 (秒) です。<c>MoveTo</c> などはここから続きます。
    /// </summary>
    public float Cursor { get; private set; }

    public FilmmakerInterpolation Interpolation { get; set; } = FilmmakerInterpolation.Spline;

    public float EyeHeight { get; set; } = FilmmakerAnimationClip.DefaultEyeHeight;

    /// <summary>
    /// 注視点とカメラの間に壁があれば、カメラを手前に寄せます。
    /// </summary>
    public bool AvoidWalls { get; set; } = true;

    /// <summary>
    /// 壁に寄せたときに残す隙間です。
    /// </summary>
    public float WallClearance { get; set; } = 0.35f;

    /// <summary>
    /// 役者として出すときの既定の役職です。
    /// </summary>
    public RoleTypeId? ActorRole { get; set; }

    /// <summary>
    /// 役者に持たせるアイテムです。
    /// </summary>
    public List<ItemType> ActorItems { get; } = [];

    public bool HasKeys => _positions.Count > 0;

    /// <summary>
    /// 最後のカメラ位置です。
    /// </summary>
    public Vector3 LastPosition
    {
        get
        {
            RequireKeys();
            return _positions[_positions.Count - 1].Value;
        }
    }

    /// <summary>
    /// 最後の注視点です。
    /// </summary>
    public Vector3 LastLookAt
    {
        get
        {
            RequireKeys();
            return _looks[_looks.Count - 1].Value;
        }
    }

    /// <summary>
    /// 指定秒にカメラを置きます。
    /// </summary>
    public FilmmakerAnimationBuilder Key(
        float seconds,
        Vector3 position,
        Vector3 lookAt,
        FilmmakerBlendPreset blend = FilmmakerBlendPreset.Linear)
    {
        int frame = ToFrame(seconds);
        position = ResolveWalls(position, lookAt);
        AddOrReplace(_positions, FilmmakerVector3Keyframe.From(frame, blend, position));
        AddOrReplace(_looks, FilmmakerVector3Keyframe.From(frame, blend, lookAt));
        Cursor = Mathf.Max(Cursor, frame / FrameRate);
        return this;
    }

    /// <summary>
    /// 指定秒に、向きでカメラを置きます。
    /// </summary>
    public FilmmakerAnimationBuilder Key(
        float seconds,
        Vector3 position,
        Quaternion rotation,
        FilmmakerBlendPreset blend = FilmmakerBlendPreset.Linear)
        => Key(seconds, position, LookTargetOf(position, rotation), blend);

    /// <summary>
    /// 直前のキーから <paramref name="duration"/> 秒かけて移動します。
    /// <see cref="FilmmakerBlendPreset.Smooth"/> なら出だしと終わりで減速します。
    /// </summary>
    public FilmmakerAnimationBuilder MoveTo(
        float duration,
        Vector3 position,
        Vector3 lookAt,
        FilmmakerBlendPreset blend = FilmmakerBlendPreset.Linear)
    {
        RequireKeys();
        SetLastBlend(blend);
        return Key(Cursor + Mathf.Max(1f / FrameRate, duration), position, lookAt);
    }

    public FilmmakerAnimationBuilder MoveTo(
        float duration,
        Vector3 position,
        Quaternion rotation,
        FilmmakerBlendPreset blend = FilmmakerBlendPreset.Linear)
        => MoveTo(duration, position, LookTargetOf(position, rotation), blend);

    /// <summary>
    /// カメラは動かさず、注視点だけを移します。
    /// </summary>
    public FilmmakerAnimationBuilder PanTo(
        float duration,
        Vector3 lookAt,
        FilmmakerBlendPreset blend = FilmmakerBlendPreset.Smooth)
        => MoveTo(duration, LastPosition, lookAt, blend);

    /// <summary>
    /// 直前の構図のまま止まります。止まる前後は減速・加速します。
    /// </summary>
    public FilmmakerAnimationBuilder Wait(float duration)
    {
        Vector3 position = LastPosition;
        Vector3 lookAt = LastLookAt;
        SetLastBlend(FilmmakerBlendPreset.Hold);
        return Key(Cursor + Mathf.Max(1f / FrameRate, duration), position, lookAt);
    }

    /// <summary>
    /// 別の構図へ瞬時に切り替えます (カット)。
    /// </summary>
    public FilmmakerAnimationBuilder Cut(Vector3 position, Vector3 lookAt)
    {
        if (!HasKeys)
            return Key(0f, position, lookAt);

        SetLastBlend(FilmmakerBlendPreset.Hold);
        return Key(Cursor + 1f / FrameRate, position, lookAt);
    }

    /// <summary>
    /// 経過秒からカメラ位置と注視点を返す関数を焼き込みます。キーで書きにくい動きに使います。
    /// </summary>
    /// <param name="duration">この区間の長さ (秒)。</param>
    /// <param name="position">区間の経過秒 (0〜<paramref name="duration"/>) からカメラ位置を返します。</param>
    /// <param name="lookAt">区間の経過秒から注視点を返します。</param>
    /// <param name="interval">焼き込む間隔 (秒)。曲線で繋ぐので粗くても滑らかです。</param>
    public FilmmakerAnimationBuilder Path(
        float duration,
        Func<float, Vector3> position,
        Func<float, Vector3> lookAt,
        float interval = DefaultPathInterval)
    {
        if (position == null)
            throw new ArgumentNullException(nameof(position));
        if (lookAt == null)
            throw new ArgumentNullException(nameof(lookAt));

        duration = Mathf.Max(0f, duration);
        interval = Mathf.Max(1f / FrameRate, interval);

        if (HasKeys)
            SetLastBlend(FilmmakerBlendPreset.Linear);

        float start = Cursor;
        int steps = Mathf.Max(1, Mathf.CeilToInt(duration / interval));
        for (int i = 0; i <= steps; i++)
        {
            float t = duration * i / steps;
            Key(start + t, position(t), lookAt(t));
        }

        return this;
    }

    /// <inheritdoc cref="Path(float, Func{float, Vector3}, Func{float, Vector3}, float)"/>
    public FilmmakerAnimationBuilder Path(float duration, Func<float, Pose> pose, float interval = DefaultPathInterval)
    {
        if (pose == null)
            throw new ArgumentNullException(nameof(pose));

        return Path(
            duration,
            t => pose(t).position,
            t =>
            {
                Pose sample = pose(t);
                return LookTargetOf(sample.position, sample.rotation);
            },
            interval);
    }

    /// <summary>
    /// <paramref name="center"/> の周りを回りながら <paramref name="lookAt"/> を見ます。
    /// </summary>
    /// <param name="basis">0 度の方向です。0 度は basis の前方 (z+) です。</param>
    /// <param name="easeInOut">出だしと終わりで減速するかどうか。</param>
    public FilmmakerAnimationBuilder Orbit(
        float duration,
        Vector3 center,
        Quaternion basis,
        float fromDegrees,
        float toDegrees,
        float radius,
        float height,
        Vector3 lookAt,
        bool easeInOut = true)
        => Path(
            duration,
            t =>
            {
                float progress = duration > 0f ? t / duration : 1f;
                if (easeInOut)
                    progress = Mathf.SmoothStep(0f, 1f, progress);

                float degrees = Mathf.Lerp(fromDegrees, toDegrees, progress);
                return center + basis * (Quaternion.Euler(0f, degrees, 0f) * new Vector3(0f, height, radius));
            },
            _ => lookAt);

    /// <summary>
    /// 直前の構図のまま画面を揺らします。揺れは終わりに向かって収まります。
    /// </summary>
    /// <param name="strength">注視点を揺らす幅 (m) です。</param>
    /// <param name="frequency">1 秒あたりの揺れの細かさです。</param>
    public FilmmakerAnimationBuilder Shake(float duration, float strength = 0.25f, float frequency = 18f)
    {
        Vector3 position = LastPosition;
        Vector3 lookAt = LastLookAt;
        float seed = UnityEngine.Random.value * 100f;

        return Path(
            duration,
            t => position + Noise(seed + 7f, t * frequency) * (strength * 0.15f * Decay(t, duration)),
            t => lookAt + Noise(seed, t * frequency) * (strength * Decay(t, duration)),
            1f / Mathf.Max(5f, frequency));
    }

    /// <summary>
    /// 今の最後 (<see cref="Cursor"/>) に合図を置きます。
    /// </summary>
    public FilmmakerAnimationBuilder Cue(string name) => Cue(Cursor, name);

    /// <summary>
    /// 指定秒に合図を置きます。再生中にこの時刻で <see cref="FilmmakerCutscene.CueReached"/> が起きます。
    /// </summary>
    public FilmmakerAnimationBuilder Cue(float seconds, string name)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("合図の名前が空です。", nameof(name));

        int frame = ToFrame(seconds);
        _cues.Add(FilmmakerCue.From(frame, name));
        Cursor = Mathf.Max(Cursor, frame / FrameRate);
        return this;
    }

    /// <summary>
    /// 今の最後 (<see cref="Cursor"/>) に、役者が実行するダミーアクションを置きます。
    /// </summary>
    public FilmmakerAnimationBuilder Act(string path) => Act(Cursor, path);

    /// <summary>
    /// 指定秒に、役者が実行するダミーアクション (<see cref="DummyActions"/> のパス) を置きます。
    /// </summary>
    public FilmmakerAnimationBuilder Act(float seconds, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("アクションが空です。", nameof(path));

        int frame = ToFrame(seconds);
        _actions.Add(FilmmakerActionKey.From(frame, path));
        Cursor = Mathf.Max(Cursor, frame / FrameRate);
        return this;
    }

    /// <summary>
    /// 別のクリップを今の最後に続けます。記録したクリップもそのまま繋げます。
    /// </summary>
    public FilmmakerAnimationBuilder Append(FilmmakerAnimationClip clip)
    {
        if (clip == null)
            throw new ArgumentNullException(nameof(clip));

        clip.Normalize();

        float offset = HasKeys ? Cursor + 1f / FrameRate : 0f;
        float first = clip.FirstFrame / clip.EffectiveFrameRate;

        // 元クリップのキー時刻で姿勢を取り直し、位置と注視点の 2 本に揃える。
        IEnumerable<int> frames = clip.Positions.Select(key => key.TimeFrames)
            .Concat(clip.Rotations.Select(key => key.TimeFrames))
            .Concat(clip.LookTargets.Select(key => key.TimeFrames))
            .Distinct()
            .OrderBy(frame => frame);

        if (HasKeys)
            SetLastBlend(FilmmakerBlendPreset.Hold);

        bool avoidWalls = AvoidWalls;
        AvoidWalls = false;
        try
        {
            foreach (int frame in frames)
            {
                float seconds = frame / clip.EffectiveFrameRate;
                if (!clip.TryEvaluate(seconds, out FilmmakerAnimationSample sample) || !sample.HasPosition)
                    continue;

                Quaternion rotation = sample.HasRotation ? sample.Rotation : Quaternion.identity;
                Key(offset + seconds - first, sample.Position, LookTargetOf(sample.Position, rotation));
            }
        }
        finally
        {
            AvoidWalls = avoidWalls;
        }

        foreach (FilmmakerCue cue in clip.Cues)
            Cue(offset + cue.TimeFrames / clip.EffectiveFrameRate - first, cue.Name);

        foreach (FilmmakerActionKey action in clip.Actions)
            Act(offset + action.TimeFrames / clip.EffectiveFrameRate - first, action.Path);

        ActorRole ??= clip.ActorRole;
        foreach (ItemType item in clip.ActorItems)
        {
            if (!ActorItems.Contains(item))
                ActorItems.Add(item);
        }

        return this;
    }

    /// <summary>
    /// 保存済み・コード定義のクリップを名前で続けます。
    /// </summary>
    public FilmmakerAnimationBuilder Append(string animationName)
    {
        if (!FilmmakerAnimation.TryResolve(animationName, out FilmmakerAnimationClip clip, out string error))
            throw new InvalidOperationException(error);

        return Append(clip);
    }

    public FilmmakerAnimationClip Build()
    {
        var clip = new FilmmakerAnimationClip
        {
            Name = _name,
            Source = nameof(FilmmakerAnimationBuilder),
            FrameRate = FrameRate,
            Interpolation = Interpolation,
            EyeHeight = EyeHeight,
            Positions = [.. _positions],
            LookTargets = [.. _looks],
            Zooms = [.. _zooms],
            Cues = [.. _cues],
            Actions = [.. _actions],
            ActorRole = ActorRole,
            ActorItems = [.. ActorItems],
        };

        clip.Normalize();
        return clip;
    }

    /// <summary>
    /// 目線から <paramref name="lookAt"/> を向く回転です。
    /// </summary>
    public Quaternion LookFrom(Vector3 position, Vector3 lookAt)
    {
        Vector3 direction = lookAt - (position + Vector3.up * EyeHeight);
        return direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : Quaternion.identity;
    }

    /// <summary>
    /// <paramref name="from"/> から <paramref name="to"/> を見通せないとき、<paramref name="to"/> を手前の壁際に寄せます。
    /// </summary>
    public static Vector3 PullInFromWalls(Vector3 from, Vector3 to, float clearance)
    {
        Vector3 direction = to - from;
        float distance = direction.magnitude;
        if (distance < 0.01f)
            return to;

        direction /= distance;
        if (!Physics.SphereCast(from, 0.15f, direction, out RaycastHit hit, distance, FpcStateProcessor.Mask, QueryTriggerInteraction.Ignore))
            return to;

        return from + direction * Mathf.Max(0f, hit.distance - clearance);
    }

    private Vector3 ResolveWalls(Vector3 position, Vector3 lookAt)
    {
        if (!AvoidWalls)
            return position;

        Vector3 eye = position + Vector3.up * EyeHeight;
        return PullInFromWalls(lookAt, eye, WallClearance) - Vector3.up * EyeHeight;
    }

    private Vector3 LookTargetOf(Vector3 position, Quaternion rotation)
        => position + Vector3.up * EyeHeight + rotation * Vector3.forward * LookDistance;

    private static Vector3 Noise(float seed, float t)
        => new(
            Mathf.PerlinNoise(seed, t) - 0.5f,
            Mathf.PerlinNoise(seed + 31.7f, t) - 0.5f,
            Mathf.PerlinNoise(seed + 63.1f, t) - 0.5f);

    private static float Decay(float t, float duration)
        => duration > 0f ? 1f - Mathf.Clamp01(t / duration) : 0f;

    private static int ToFrame(float seconds)
        => Mathf.RoundToInt(Mathf.Max(0f, seconds) * FrameRate);

    private static void AddOrReplace<TFrame>(List<TFrame> frames, TFrame frame)
        where TFrame : FilmmakerAnimationKeyframe
    {
        int index = frames.FindIndex(existing => existing.TimeFrames == frame.TimeFrames);
        if (index >= 0)
        {
            frames[index] = frame;
            return;
        }

        frames.Add(frame);
        if (frames.Count > 1 && frames[frames.Count - 2].TimeFrames > frame.TimeFrames)
            frames.Sort((a, b) => a.TimeFrames.CompareTo(b.TimeFrames));
    }

    private void SetLastBlend(FilmmakerBlendPreset blend)
    {
        _positions[_positions.Count - 1].Blend = blend;
        _looks[_looks.Count - 1].Blend = blend;
    }

    private void RequireKeys()
    {
        if (!HasKeys)
            throw new InvalidOperationException("最初に Key で開始の構図を置いてください。");
    }
}
