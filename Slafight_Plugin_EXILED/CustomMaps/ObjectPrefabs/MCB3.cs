using System;
using System.Collections.Generic;
using Exiled.API.Features;
using LabApi.Events.Arguments.PlayerEvents;
using MEC;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

/// <summary>
/// 設置型MCB3。INTR/0の操作完了、または武器攻撃で耐久値が尽きると破壊される。
/// </summary>
public sealed class MCB3 : DestructibleObjectPrefab
{
    private const string InteractableKey = "INTR";
    private const string InteractableTag = "0";
    private const float AnimationTick = 0.02f;

    private CoroutineHandle _animationCoroutine;
    private InteractableHandle? _destroyInteractable;
    private bool _isReady;
    private bool _isDestroying;

    protected override string SchematicName => "MCB3";

    protected override float SetupDelay => 0f;

    protected override string CustomHitboxGroup => "MCB3";

    /// <summary>攻撃で破壊されるまでの耐久値。ObjectPrefab Optionから変更可能。</summary>
    public override float Durability { get; set; } = 400f;

    /// <summary>フェードと上下移動にかける秒数。</summary>
    public float AnimationDuration { get; set; } = 0.7f;

    /// <summary>出現元の上方向、および消滅先の下方向への移動距離。</summary>
    public float AnimationTravelDistance { get; set; } = 0.65f;

    /// <summary>設置時にAudioReferencesから再生する音声ファイル。</summary>
    public string PlacementSoundFile { get; set; } = "MCB3_Setup.ogg";

    /// <summary>破壊時にAudioReferencesから再生する音声ファイル。</summary>
    public string DestructionSoundFile { get; set; } = "MCB3_Setup.ogg";

    protected override void OnCreate()
    {
        if (Schematic == null)
            return;

        Schematic.SetCollidable(false);
        Schematic.SetOpacity(0f);
        Schematic.Position = Position + Vector3.down * Math.Max(0f, AnimationTravelDistance);
        PlaySound(PlacementSoundFile, "place");
        _animationCoroutine = Timing.RunCoroutine(AnimateIn());
    }

    protected override void OnDestructibleSetup()
    {
        CustomHitbox?.SetEnabled(_isReady);

        var block = GetBlock(InteractableKey);
        if (block == null || !string.Equals(block.ObjectPrefabTag, InteractableTag, StringComparison.OrdinalIgnoreCase))
        {
            Log.Warn($"[MCB3] Interactable Key='{InteractableKey}', Tag='{InteractableTag}' が見つかりません。");
            return;
        }

        _destroyInteractable = GetInteractable(InteractableKey);
        if (_destroyInteractable == null)
        {
            Log.Warn($"[MCB3] managed Interactable '{InteractableKey}' を取得できません。");
            return;
        }

        _destroyInteractable.Enabled = _isReady;
        _destroyInteractable.Interacted += OnDestroyInteracted;
    }

    public override void Destroy()
    {
        if (_isDestroying)
            return;

        if (Schematic == null || AnimationDuration <= 0f)
        {
            DestroyImmediately();
            return;
        }

        _isDestroying = true;
        _isReady = false;
        CustomHitbox?.SetEnabled(false);
        if (_destroyInteractable != null)
            _destroyInteractable.Enabled = false;

        if (_animationCoroutine.IsRunning)
            Timing.KillCoroutines(_animationCoroutine);

        Schematic.SetCollidable(false);
        PlaySound(DestructionSoundFile, "destroy");
        _animationCoroutine = Timing.RunCoroutine(AnimateOut());
    }

    /// <summary>ラウンド終了や生成失敗時にアニメーションを待たず確実に解放する。</summary>
    public void DestroyImmediately()
    {
        _isDestroying = true;
        _isReady = false;
        CustomHitbox?.SetEnabled(false);

        if (_animationCoroutine.IsRunning)
            Timing.KillCoroutines(_animationCoroutine);

        _animationCoroutine = default;
        UnsubscribeInteractable();
        base.Destroy();
    }

    protected override void OnDestroy()
    {
        if (_animationCoroutine.IsRunning)
            Timing.KillCoroutines(_animationCoroutine);

        _animationCoroutine = default;
        UnsubscribeInteractable();
        base.OnDestroy();
    }

    protected override void OnRoundRestarting()
    {
        if (_animationCoroutine.IsRunning)
            Timing.KillCoroutines(_animationCoroutine);

        _animationCoroutine = default;
        _isReady = false;
        _isDestroying = true;
        CustomHitbox?.SetEnabled(false);
        UnsubscribeInteractable();
        base.OnRoundRestarting();
    }

    private IEnumerator<float> AnimateIn()
    {
        if (Schematic == null)
            yield break;

        float duration = Math.Max(AnimationTick, AnimationDuration);
        float elapsed = 0f;
        Vector3 start = Position + Vector3.down * Math.Max(0f, AnimationTravelDistance);

        while (elapsed < duration && Schematic != null && !_isDestroying)
        {
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            Schematic.Position = Vector3.Lerp(start, Position, progress);
            Schematic.SetOpacity(progress);
            elapsed += AnimationTick;
            yield return Timing.WaitForSeconds(AnimationTick);
        }

        if (Schematic == null || _isDestroying)
            yield break;

        Schematic.Position = Position;
        Schematic.RestoreOriginalState();
        _isReady = true;
        CustomHitbox?.SetEnabled(true);
        if (_destroyInteractable != null)
            _destroyInteractable.Enabled = true;

        _animationCoroutine = default;
    }

    private IEnumerator<float> AnimateOut()
    {
        if (Schematic == null)
        {
            _animationCoroutine = default;
            base.Destroy();
            yield break;
        }

        float duration = Math.Max(AnimationTick, AnimationDuration);
        float elapsed = 0f;
        Vector3 start = Schematic.Position;
        Vector3 end = Position + Vector3.down * Math.Max(0f, AnimationTravelDistance);

        while (elapsed < duration && Schematic != null)
        {
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            Schematic.Position = Vector3.Lerp(start, end, progress);
            Schematic.SetOpacity(1f - progress);
            elapsed += AnimationTick;
            yield return Timing.WaitForSeconds(AnimationTick);
        }

        _animationCoroutine = default;
        UnsubscribeInteractable();
        base.Destroy();
    }

    private void OnDestroyInteracted(Player player, PlayerSearchedToyEventArgs ev) => Destroy();

    private void UnsubscribeInteractable()
    {
        if (_destroyInteractable != null)
            _destroyInteractable.Interacted -= OnDestroyInteracted;

        _destroyInteractable = null;
    }

    private void PlaySound(string fileName, string phase)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return;

        SpeakerApi.Play(
            fileName,
            $"mcb3-{ObjectInstanceID}-{phase}",
            Position,
            destroyOnEnd: true,
            isSpatial: true,
            maxDistance: 15f,
            minDistance: 1f);
    }
}
