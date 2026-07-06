using System;
using System.Collections.Generic;
using Exiled.API.Features;
using MEC;
using PlayerRoles.FirstPersonControl;
using ProjectMER.Features.Objects;
using RelativePositioning;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Features;

public enum AnimationEase
{
    Linear,
    SmoothStep,
    SmootherStep,
    EaseInQuad,
    EaseOutQuad,
    EaseInOutQuad,
    EaseInCubic,
    EaseOutCubic,
    EaseInOutCubic,
    EaseInSine,
    EaseOutSine,
    EaseInOutSine,
}

public sealed class AnimationOptions
{
    public AnimationEase Ease { get; set; } = AnimationEase.Linear;
    public bool StopOnRoundEnd { get; set; } = true;
    public bool SnapToEnd { get; set; } = true;
    public bool UseUnscaledTime { get; set; }
    public Func<bool>? CanContinue { get; set; }
    public Action<float>? OnFrame { get; set; }
    public Action? OnComplete { get; set; }
}

public static class AnimationApi
{
    public static CoroutineHandle MoveTo(Transform? transform, Vector3 endPosition, float duration, AnimationOptions? options = null)
        => transform == null ? default : Timing.RunCoroutine(MoveToCoroutine(transform, endPosition, duration, options));

    public static CoroutineHandle MoveTo(Transform? transform, Vector3 startPosition, Vector3 endPosition, float duration, AnimationOptions? options = null)
        => transform == null ? default : Timing.RunCoroutine(MoveToCoroutine(transform, startPosition, endPosition, duration, options));

    public static IEnumerator<float> MoveToCoroutine(Transform? transform, Vector3 endPosition, float duration, AnimationOptions? options = null)
    {
        if (transform == null)
            yield break;

        yield return Timing.WaitUntilDone(MoveToCoroutine(transform, transform.position, endPosition, duration, options));
    }

    public static IEnumerator<float> MoveToCoroutine(Transform? transform, Vector3 startPosition, Vector3 endPosition, float duration, AnimationOptions? options = null)
        => AnimateVector3Core(
            value => transform!.position = value,
            startPosition,
            endPosition,
            duration,
            options,
            () => transform != null);

    public static CoroutineHandle MoveBy(Transform? transform, Vector3 offset, float duration, AnimationOptions? options = null)
        => transform == null ? default : MoveTo(transform, transform.position, transform.position + offset, duration, options);

    public static CoroutineHandle MoveBy(Transform? transform, Vector3 startPosition, Vector3 offset, float duration, AnimationOptions? options = null)
        => MoveTo(transform, startPosition, startPosition + offset, duration, options);

    public static IEnumerator<float> MoveByCoroutine(Transform? transform, Vector3 offset, float duration, AnimationOptions? options = null)
    {
        if (transform == null)
            yield break;

        yield return Timing.WaitUntilDone(MoveByCoroutine(transform, transform.position, offset, duration, options));
    }

    public static IEnumerator<float> MoveByCoroutine(Transform? transform, Vector3 startPosition, Vector3 offset, float duration, AnimationOptions? options = null)
        => MoveToCoroutine(transform, startPosition, startPosition + offset, duration, options);

    public static CoroutineHandle MoveTo(SchematicObject? schematic, Vector3 endPosition, float duration, AnimationOptions? options = null)
        => MoveTo(schematic?.transform, endPosition, duration, options);

    public static CoroutineHandle MoveTo(SchematicObject? schematic, Vector3 startPosition, Vector3 endPosition, float duration, AnimationOptions? options = null)
        => MoveTo(schematic?.transform, startPosition, endPosition, duration, options);

    public static IEnumerator<float> MoveToCoroutine(SchematicObject? schematic, Vector3 endPosition, float duration, AnimationOptions? options = null)
        => MoveToCoroutine(schematic?.transform, endPosition, duration, options);

    public static IEnumerator<float> MoveToCoroutine(SchematicObject? schematic, Vector3 startPosition, Vector3 endPosition, float duration, AnimationOptions? options = null)
        => MoveToCoroutine(schematic?.transform, startPosition, endPosition, duration, options);

    public static CoroutineHandle MoveBy(SchematicObject? schematic, Vector3 offset, float duration, AnimationOptions? options = null)
        => MoveBy(schematic?.transform, offset, duration, options);

    public static CoroutineHandle MoveBy(SchematicObject? schematic, Vector3 startPosition, Vector3 offset, float duration, AnimationOptions? options = null)
        => MoveBy(schematic?.transform, startPosition, offset, duration, options);

    public static IEnumerator<float> MoveByCoroutine(SchematicObject? schematic, Vector3 offset, float duration, AnimationOptions? options = null)
        => MoveByCoroutine(schematic?.transform, offset, duration, options);

    public static IEnumerator<float> MoveByCoroutine(SchematicObject? schematic, Vector3 startPosition, Vector3 offset, float duration, AnimationOptions? options = null)
        => MoveByCoroutine(schematic?.transform, startPosition, offset, duration, options);

    public static CoroutineHandle MoveTo(ObjectPrefab? prefab, Vector3 endPosition, float duration, AnimationOptions? options = null)
        => prefab == null ? default : Timing.RunCoroutine(MoveToCoroutine(prefab, prefab.Position, endPosition, duration, options));

    public static CoroutineHandle MoveTo(ObjectPrefab? prefab, Vector3 startPosition, Vector3 endPosition, float duration, AnimationOptions? options = null)
        => Timing.RunCoroutine(MoveToCoroutine(prefab, startPosition, endPosition, duration, options));

    public static IEnumerator<float> MoveToCoroutine(ObjectPrefab? prefab, Vector3 startPosition, Vector3 endPosition, float duration, AnimationOptions? options = null)
        => AnimateVector3Core(
            value => prefab!.Position = value,
            startPosition,
            endPosition,
            duration,
            options,
            () => prefab != null);

    public static IEnumerator<float> MoveToCoroutine(ObjectPrefab? prefab, Vector3 endPosition, float duration, AnimationOptions? options = null)
    {
        if (prefab == null)
            yield break;

        yield return Timing.WaitUntilDone(MoveToCoroutine(prefab, prefab.Position, endPosition, duration, options));
    }

    public static CoroutineHandle MoveBy(ObjectPrefab? prefab, Vector3 offset, float duration, AnimationOptions? options = null)
        => prefab == null ? default : MoveTo(prefab, prefab.Position, prefab.Position + offset, duration, options);

    public static CoroutineHandle MoveBy(ObjectPrefab? prefab, Vector3 startPosition, Vector3 offset, float duration, AnimationOptions? options = null)
        => MoveTo(prefab, startPosition, startPosition + offset, duration, options);

    public static IEnumerator<float> MoveByCoroutine(ObjectPrefab? prefab, Vector3 offset, float duration, AnimationOptions? options = null)
    {
        if (prefab == null)
            yield break;

        yield return Timing.WaitUntilDone(MoveByCoroutine(prefab, prefab.Position, offset, duration, options));
    }

    public static IEnumerator<float> MoveByCoroutine(ObjectPrefab? prefab, Vector3 startPosition, Vector3 offset, float duration, AnimationOptions? options = null)
        => MoveToCoroutine(prefab, startPosition, startPosition + offset, duration, options);

    public static CoroutineHandle MoveTo(Player? player, Vector3 endPosition, float duration, AnimationOptions? options = null)
        => player == null ? default : Timing.RunCoroutine(MoveToCoroutine(player, player.Position, endPosition, duration, options));

    public static CoroutineHandle MoveTo(Player? player, Vector3 startPosition, Vector3 endPosition, float duration, AnimationOptions? options = null)
        => Timing.RunCoroutine(MoveToCoroutine(player, startPosition, endPosition, duration, options));

    public static IEnumerator<float> MoveToCoroutine(Player? player, Vector3 startPosition, Vector3 endPosition, float duration, AnimationOptions? options = null)
        => AnimateVector3Core(
            value => SetPlayerPosition(player, value),
            startPosition,
            endPosition,
            duration,
            options,
            () => player is { IsConnected: true });

    public static IEnumerator<float> MoveToCoroutine(Player? player, Vector3 endPosition, float duration, AnimationOptions? options = null)
    {
        if (player == null)
            yield break;

        yield return Timing.WaitUntilDone(MoveToCoroutine(player, player.Position, endPosition, duration, options));
    }

    public static CoroutineHandle MoveBy(Player? player, Vector3 offset, float duration, AnimationOptions? options = null)
        => player == null ? default : MoveTo(player, player.Position, player.Position + offset, duration, options);

    public static CoroutineHandle MoveBy(Player? player, Vector3 startPosition, Vector3 offset, float duration, AnimationOptions? options = null)
        => MoveTo(player, startPosition, startPosition + offset, duration, options);

    public static IEnumerator<float> MoveByCoroutine(Player? player, Vector3 offset, float duration, AnimationOptions? options = null)
    {
        if (player == null)
            yield break;

        yield return Timing.WaitUntilDone(MoveByCoroutine(player, player.Position, offset, duration, options));
    }

    public static IEnumerator<float> MoveByCoroutine(Player? player, Vector3 startPosition, Vector3 offset, float duration, AnimationOptions? options = null)
        => MoveToCoroutine(player, startPosition, startPosition + offset, duration, options);

    public static CoroutineHandle RotateTo(Transform? transform, Quaternion endRotation, float duration, AnimationOptions? options = null)
        => transform == null ? default : Timing.RunCoroutine(RotateToCoroutine(transform, transform.rotation, endRotation, duration, options));

    public static IEnumerator<float> RotateToCoroutine(Transform? transform, Quaternion endRotation, float duration, AnimationOptions? options = null)
    {
        if (transform == null)
            yield break;

        yield return Timing.WaitUntilDone(RotateToCoroutine(transform, transform.rotation, endRotation, duration, options));
    }

    public static IEnumerator<float> RotateToCoroutine(Transform? transform, Quaternion startRotation, Quaternion endRotation, float duration, AnimationOptions? options = null)
        => AnimateQuaternionCore(
            value => transform!.rotation = value,
            startRotation,
            endRotation,
            duration,
            options,
            () => transform != null);

    public static CoroutineHandle RotateTo(SchematicObject? schematic, Quaternion endRotation, float duration, AnimationOptions? options = null)
        => RotateTo(schematic?.transform, endRotation, duration, options);

    public static IEnumerator<float> RotateToCoroutine(SchematicObject? schematic, Quaternion endRotation, float duration, AnimationOptions? options = null)
        => RotateToCoroutine(schematic?.transform, endRotation, duration, options);

    public static IEnumerator<float> RotateToCoroutine(SchematicObject? schematic, Quaternion startRotation, Quaternion endRotation, float duration, AnimationOptions? options = null)
        => RotateToCoroutine(schematic?.transform, startRotation, endRotation, duration, options);

    public static CoroutineHandle ScaleTo(Transform? transform, Vector3 endScale, float duration, AnimationOptions? options = null)
        => transform == null ? default : Timing.RunCoroutine(ScaleToCoroutine(transform, transform.localScale, endScale, duration, options));

    public static IEnumerator<float> ScaleToCoroutine(Transform? transform, Vector3 endScale, float duration, AnimationOptions? options = null)
    {
        if (transform == null)
            yield break;

        yield return Timing.WaitUntilDone(ScaleToCoroutine(transform, transform.localScale, endScale, duration, options));
    }

    public static IEnumerator<float> ScaleToCoroutine(Transform? transform, Vector3 startScale, Vector3 endScale, float duration, AnimationOptions? options = null)
        => AnimateVector3Core(
            value => transform!.localScale = value,
            startScale,
            endScale,
            duration,
            options,
            () => transform != null);

    public static CoroutineHandle ScaleTo(SchematicObject? schematic, Vector3 endScale, float duration, AnimationOptions? options = null)
        => ScaleTo(schematic?.transform, endScale, duration, options);

    public static IEnumerator<float> ScaleToCoroutine(SchematicObject? schematic, Vector3 endScale, float duration, AnimationOptions? options = null)
        => ScaleToCoroutine(schematic?.transform, endScale, duration, options);

    public static IEnumerator<float> ScaleToCoroutine(SchematicObject? schematic, Vector3 startScale, Vector3 endScale, float duration, AnimationOptions? options = null)
        => ScaleToCoroutine(schematic?.transform, startScale, endScale, duration, options);

    public static CoroutineHandle AnimateFloat(Func<float> getter, Action<float> setter, float endValue, float duration, AnimationOptions? options = null)
    {
        if (getter == null)
            throw new ArgumentNullException(nameof(getter));

        return Timing.RunCoroutine(AnimateFloatCoroutine(getter, setter, getter(), endValue, duration, options));
    }

    public static CoroutineHandle AnimateFloat(Func<float> getter, Action<float> setter, float startValue, float endValue, float duration, AnimationOptions? options = null)
        => Timing.RunCoroutine(AnimateFloatCoroutine(getter, setter, startValue, endValue, duration, options));

    public static IEnumerator<float> AnimateFloatCoroutine(Func<float> getter, Action<float> setter, float startValue, float endValue, float duration, AnimationOptions? options = null)
    {
        if (getter == null)
            throw new ArgumentNullException(nameof(getter));
        if (setter == null)
            throw new ArgumentNullException(nameof(setter));

        return AnimateFloatCore(setter, startValue, endValue, duration, options, null);
    }

    public static CoroutineHandle AnimateVector3(Func<Vector3> getter, Action<Vector3> setter, Vector3 endValue, float duration, AnimationOptions? options = null)
    {
        if (getter == null)
            throw new ArgumentNullException(nameof(getter));

        return Timing.RunCoroutine(AnimateVector3Coroutine(getter, setter, getter(), endValue, duration, options));
    }

    public static CoroutineHandle AnimateVector3(Func<Vector3> getter, Action<Vector3> setter, Vector3 startValue, Vector3 endValue, float duration, AnimationOptions? options = null)
        => Timing.RunCoroutine(AnimateVector3Coroutine(getter, setter, startValue, endValue, duration, options));

    public static IEnumerator<float> AnimateVector3Coroutine(Func<Vector3> getter, Action<Vector3> setter, Vector3 startValue, Vector3 endValue, float duration, AnimationOptions? options = null)
    {
        if (getter == null)
            throw new ArgumentNullException(nameof(getter));
        if (setter == null)
            throw new ArgumentNullException(nameof(setter));

        return AnimateVector3Core(setter, startValue, endValue, duration, options, null);
    }

    public static CoroutineHandle AnimateQuaternion(Func<Quaternion> getter, Action<Quaternion> setter, Quaternion endValue, float duration, AnimationOptions? options = null)
    {
        if (getter == null)
            throw new ArgumentNullException(nameof(getter));

        return Timing.RunCoroutine(AnimateQuaternionCoroutine(getter, setter, getter(), endValue, duration, options));
    }

    public static CoroutineHandle AnimateQuaternion(Func<Quaternion> getter, Action<Quaternion> setter, Quaternion startValue, Quaternion endValue, float duration, AnimationOptions? options = null)
        => Timing.RunCoroutine(AnimateQuaternionCoroutine(getter, setter, startValue, endValue, duration, options));

    public static IEnumerator<float> AnimateQuaternionCoroutine(Func<Quaternion> getter, Action<Quaternion> setter, Quaternion startValue, Quaternion endValue, float duration, AnimationOptions? options = null)
    {
        if (getter == null)
            throw new ArgumentNullException(nameof(getter));
        if (setter == null)
            throw new ArgumentNullException(nameof(setter));

        return AnimateQuaternionCore(setter, startValue, endValue, duration, options, null);
    }

    public static CoroutineHandle AnimateColor(Func<Color> getter, Action<Color> setter, Color endValue, float duration, AnimationOptions? options = null)
    {
        if (getter == null)
            throw new ArgumentNullException(nameof(getter));

        return Timing.RunCoroutine(AnimateColorCoroutine(getter, setter, getter(), endValue, duration, options));
    }

    public static CoroutineHandle AnimateColor(Func<Color> getter, Action<Color> setter, Color startValue, Color endValue, float duration, AnimationOptions? options = null)
        => Timing.RunCoroutine(AnimateColorCoroutine(getter, setter, startValue, endValue, duration, options));

    public static IEnumerator<float> AnimateColorCoroutine(Func<Color> getter, Action<Color> setter, Color startValue, Color endValue, float duration, AnimationOptions? options = null)
    {
        if (getter == null)
            throw new ArgumentNullException(nameof(getter));
        if (setter == null)
            throw new ArgumentNullException(nameof(setter));

        return AnimateColorCore(setter, startValue, endValue, duration, options, null);
    }

    public static float Evaluate(AnimationEase ease, float progress)
    {
        float t = Mathf.Clamp01(progress);
        return ease switch
        {
            AnimationEase.SmoothStep => t * t * (3f - 2f * t),
            AnimationEase.SmootherStep => t * t * t * (t * (6f * t - 15f) + 10f),
            AnimationEase.EaseInQuad => t * t,
            AnimationEase.EaseOutQuad => 1f - (1f - t) * (1f - t),
            AnimationEase.EaseInOutQuad => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f,
            AnimationEase.EaseInCubic => t * t * t,
            AnimationEase.EaseOutCubic => 1f - Mathf.Pow(1f - t, 3f),
            AnimationEase.EaseInOutCubic => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f,
            AnimationEase.EaseInSine => 1f - Mathf.Cos(t * Mathf.PI / 2f),
            AnimationEase.EaseOutSine => Mathf.Sin(t * Mathf.PI / 2f),
            AnimationEase.EaseInOutSine => -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f,
            _ => t,
        };
    }

    private static IEnumerator<float> AnimateFloatCore(
        Action<float> setter,
        float startValue,
        float endValue,
        float duration,
        AnimationOptions? options,
        Func<bool>? targetIsValid)
        => AnimateCore(
            setter,
            startValue,
            endValue,
            duration,
            options,
            targetIsValid,
            Mathf.LerpUnclamped);

    private static IEnumerator<float> AnimateVector3Core(
        Action<Vector3> setter,
        Vector3 startValue,
        Vector3 endValue,
        float duration,
        AnimationOptions? options,
        Func<bool>? targetIsValid)
        => AnimateCore(
            setter,
            startValue,
            endValue,
            duration,
            options,
            targetIsValid,
            Vector3.LerpUnclamped);

    private static IEnumerator<float> AnimateQuaternionCore(
        Action<Quaternion> setter,
        Quaternion startValue,
        Quaternion endValue,
        float duration,
        AnimationOptions? options,
        Func<bool>? targetIsValid)
        => AnimateCore(
            setter,
            startValue,
            endValue,
            duration,
            options,
            targetIsValid,
            Quaternion.Lerp);

    private static IEnumerator<float> AnimateColorCore(
        Action<Color> setter,
        Color startValue,
        Color endValue,
        float duration,
        AnimationOptions? options,
        Func<bool>? targetIsValid)
        => AnimateCore(
            setter,
            startValue,
            endValue,
            duration,
            options,
            targetIsValid,
            Color.Lerp);

    private static IEnumerator<float> AnimateCore<T>(
        Action<T> setter,
        T startValue,
        T endValue,
        float duration,
        AnimationOptions? options,
        Func<bool>? targetIsValid,
        Func<T, T, float, T> lerp)
    {
        options ??= new AnimationOptions();

        if (!CanContinue(options, targetIsValid))
            yield break;

        if (duration <= 0f)
        {
            if (options.SnapToEnd)
                setter(endValue);

            options.OnFrame?.Invoke(1f);
            options.OnComplete?.Invoke();
            yield break;
        }

        setter(startValue);
        options.OnFrame?.Invoke(0f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!CanContinue(options, targetIsValid))
                yield break;

            elapsed += options.UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = Evaluate(options.Ease, progress);

            setter(lerp(startValue, endValue, eased));
            options.OnFrame?.Invoke(eased);

            yield return 0f;
        }

        if (!CanContinue(options, targetIsValid))
            yield break;

        if (options.SnapToEnd)
            setter(endValue);

        options.OnFrame?.Invoke(1f);
        options.OnComplete?.Invoke();
    }

    private static bool CanContinue(AnimationOptions options, Func<bool>? targetIsValid)
    {
        if (targetIsValid != null && !targetIsValid())
            return false;

        if (options.StopOnRoundEnd && (Round.IsLobby || Round.IsEnded))
            return false;

        return options.CanContinue?.Invoke() ?? true;
    }

    private static void SetPlayerPosition(Player? player, Vector3 position)
    {
        if (player == null)
            return;

        player.Position = position;

        if (player.ReferenceHub?.roleManager?.CurrentRole is not IFpcRole fpcRole ||
            fpcRole.FpcModule?.ModuleReady != true)
        {
            return;
        }

        fpcRole.FpcModule.Motor.ReceivedPosition = new RelativePosition(position);
        fpcRole.FpcModule.Motor.Velocity = Vector3.zero;
        fpcRole.FpcModule.Motor.ResetFallDamageCooldown();
    }
}
