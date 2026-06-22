using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Warhead;
using MEC;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

using Slafight_Plugin_EXILED.API.Features;
namespace Slafight_Plugin_EXILED.CustomMaps.Features;

public static class WarheadBoomEffectHandler
{
    private static readonly List<CoroutineHandle> ExtraHandles = [];
    private static readonly List<Exiled.API.Features.Toys.Light> ExtraLights = [];

    public static void Register()
    {
        Exiled.Events.Handlers.Warhead.Starting += InvokeCoroutine;
        Exiled.Events.Handlers.Warhead.Detonated += OnDetonated;
    }

    public static void Unregister()
    {
        Exiled.Events.Handlers.Warhead.Starting -= InvokeCoroutine;
        Exiled.Events.Handlers.Warhead.Detonated -= OnDetonated;
        CleanupRunningEffects();
    }
    private static CoroutineHandle _handle;

    public static bool IsBooming = false;

    private static void OnDetonated()
    {
        CleanupExtraHandles();
        IsBooming = false;
        if (Round.IsLobby) return;
        foreach (var player in Player.List)
        {
            if (player == null || !player.IsAlive) continue;
            if (player.Position.y <= 265f)
            {
                player.Kill(DamageType.Warhead);
            }
        }
        foreach (var player in Player.List)
        {
            if (player is null || !player.IsAlive) continue;
            player.EnableEffect(EffectType.Concussed, 255, 10f);
        }
    }

    private static void InvokeCoroutine(StartingEventArgs ev)
    {
        if (!ev.IsAllowed) return;
        foreach (var player in Player.List)
        {
            if (player == null) continue;
            /*PlayerHUD.Instance.HintSync(SyncType.PHUD_Debug, "<size=18>[WarheadStatus]\n" +
                                                                     $"DetonationTimer: {Warhead.DetonationTimer}\n" +
                                                                     $"RealDetonationTimer: {Warhead.RealDetonationTimer}\n" +
                                                                     $"TimeToDetonate: {Warhead.Controller.CurScenario.TimeToDetonate}\n" +
                                                                     $"StartTime: {Warhead.Controller.Info.StartTime}" +
                                                                     "</size>", player);*/
        }
        IsBooming = false;
        Timing.KillCoroutines(_handle);
        CleanupExtraHandles();
        _handle = Timing.RunCoroutine(EffectCoroutine());
    }

    private static IEnumerator<float> EffectCoroutine()
    {
        yield return Timing.WaitForSeconds(0.1f);

        IsBooming = false;
        float startTimer    = Warhead.DetonationTimer;
        float startRealTime = Time.realtimeSinceStartup;

        while (true)
        {
            if (!Warhead.IsInProgress || Round.IsLobby) yield break;

            float elapsed            = Time.realtimeSinceStartup - startRealTime;
            float estimatedRemaining = startTimer - elapsed;

            foreach (var player in Player.List)
            {
                if (player == null) continue;
                /*
                PlayerHUD.Instance.HintSync(SyncType.PHUD_Debug, "<size=18>[WarheadStatus]\n" +
                                                                         $"DetonationTimer: {Warhead.DetonationTimer}\n" +
                                                                         $"RealDetonationTimer: {Warhead.RealDetonationTimer}\n" +
                                                                         $"TimeToDetonate: {Warhead.Controller.CurScenario.TimeToDetonate}\n" +
                                                                         $"StartTime: {Warhead.Controller.Info.StartTime}\n" +
                                                                         $"EstimatedRemaining: {estimatedRemaining:F1}" +
                                                                         "</size>", player);
                                                                         */
            }

            // ── 残り 10秒: 煙エフェクト & 音 & フラッシュ予約 ─────────
            if (estimatedRemaining <= 10f)
            {
                IsBooming = true;
                var effectPos = StaticUtils.GetWorldFromRoomLocal(
                    RoomType.HczNuke, new Vector3(30f, -80f, 0f), Vector3.zero).worldPosition;

                WarheadBoomEffectUtil.CreateAndStartEffect(effectPos, 10f, 0.15f, 0.03f);
                SpeakerApi.Play("warheaddrama.ogg", "WarheadDrama", Vector3.zero, true, null, false, 99999999f, 0f);

                // 残り 0.3秒になるまで待ってからフラッシュ
                float flashDelay = estimatedRemaining - 0.3f;
                ExtraHandles.Add(Timing.RunCoroutine(FlashAfterDelayCoroutine(effectPos, flashDelay)));

                yield break;
            }

            yield return Timing.WaitForSeconds(0.1f);
        }
    }

    // 指定秒数待ってからフラッシュを起動
    private static IEnumerator<float> FlashAfterDelayCoroutine(Vector3 position, float delay)
    {
        if (delay > 0f)
            yield return Timing.WaitForSeconds(delay);

        ExtraHandles.Add(Timing.RunCoroutine(FlashCoroutine(position)));
    }

    // ================================================================
    //  爆発フラッシュコルーチン
    //  - 0.00〜0.08s : EaseOut で Range/Intensity を最大まで急拡大
    //  - 0.08〜0.30s : EaseIn で Range/Intensity をフェードアウト
    //  - 0.30s       : Light を破棄
    // ================================================================
    private static IEnumerator<float> FlashCoroutine(Vector3 position)
    {
        const float expandDuration = 0.35f;
        const float fadeDuration   = 5.55f;
        const float maxIntensity   = 20000000f;
        const float maxRange       = 120f;
        const float tickInterval   = 0.016f; // ~60fps

        var flashColor = new Color(1f, 0.85f, 0.5f); // 白に近いオレンジ

        var light = Exiled.API.Features.Toys.Light.Create(
            position: position,
            rotation: null,
            scale:    null,
            spawn:    true,
            color:    flashColor
        );
        ExtraLights.Add(light);
        light.LightType  = LightType.Point;
        light.Intensity  = 0f;
        light.Range      = 1f;
        light.ShadowType = LightShadows.None;

        // ── フェーズ1: 拡大 ────────────────────────────────────────
        float t = 0f;
        while (t < expandDuration)
        {
            float ratio     = Mathf.Clamp01(t / expandDuration);
            float eased     = 1f - (1f - ratio) * (1f - ratio); // EaseOut
            light.Intensity = Mathf.Lerp(0f, maxIntensity, eased);
            light.Range     = Mathf.Lerp(1f, maxRange,     eased);
            t              += tickInterval;
            yield return Timing.WaitForSeconds(tickInterval);
        }
        light.Intensity = maxIntensity;
        light.Range     = maxRange;

        // ── フェーズ2: フェードアウト ──────────────────────────────
        t = 0f;
        while (t < fadeDuration)
        {
            float ratio     = Mathf.Clamp01(t / fadeDuration);
            float eased     = ratio * ratio; // EaseIn
            light.Intensity = Mathf.Lerp(maxIntensity, 0f, eased);
            light.Range     = Mathf.Lerp(maxRange,     0f, eased);
            t              += tickInterval;
            yield return Timing.WaitForSeconds(tickInterval);
        }

        // ── 後片付け ───────────────────────────────────────────────
        light.Intensity = 0f;
        light.Destroy();
        ExtraLights.Remove(light);
    }

    private static void CleanupRunningEffects()
    {
        Timing.KillCoroutines(_handle);
        _handle = default;
        CleanupExtraHandles();
        WarheadBoomEffectUtil.StopAllEffects();
        IsBooming = false;
    }

    private static void CleanupExtraHandles()
    {
        foreach (var handle in ExtraHandles)
            Timing.KillCoroutines(handle);

        ExtraHandles.Clear();

        foreach (var light in ExtraLights.ToArray())
        {
            try { light?.Destroy(); }
            catch { /* ignored */ }
        }

        ExtraLights.Clear();
    }
}
