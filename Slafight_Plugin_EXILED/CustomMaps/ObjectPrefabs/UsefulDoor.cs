using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using LabApi.Events.Arguments.PlayerEvents;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Player = Exiled.API.Features.Player;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

/// <summary>
/// A configurable, permission-aware schematic door. Direct interactions and
/// button dispatches both enter <see cref="TryInteract(UDoorInteractionContext)"/>.
/// </summary>
public class UsefulDoor : ObjectPrefab
{
    public const string CentralSchematicName = "UsefulDoor";
    public const string InteractableKey = "Interactable";
    private static readonly Vector3 InteractableLocalOffset = Vector3.up * 0.75f;
    private static readonly Vector3 InteractableBaseScale = Vector3.one + Vector3.up * 2f - new Vector3(-0.8f, 0f, -0.8f);
    private static readonly Vector3 HiddenModelOffset = Vector3.down * 2000f;

    private UDoorModelType _modelType = UDoorModelType.Alpha;
    private string _customModelKey = string.Empty;
    private bool _isOpen;
    private bool _isSetup;
    private int _buttonStateRevision;
    private InteractableHandle? _interactable;
    private InteractableHandle? _fallbackInteractable;
    private SpeakerApi.Playback _idlePlayback;

    protected override string SchematicName => CentralSchematicName;

    public UDoorModelType ModelType
    {
        get => _modelType;
        set
        {
            bool changed = _modelType != value;
            _modelType = value;
            if (UseModelTypeDefaults)
                ApplyDefaultsForModelType();

            if (!changed)
                return;

            ApplyModelVisibility();
            if (_isSetup)
                ApplyVisualState(_isOpen, playAction: false);
        }
    }

    /// <summary>When enabled, changing ModelType applies that type's animation/audio defaults.</summary>
    public bool UseModelTypeDefaults { get; set; } = true;

    /// <summary>Exact ObjectPrefabSchematicInfo key used when ModelType is Custom.</summary>
    public string CustomModelKey
    {
        get => _customModelKey;
        set
        {
            string normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(_customModelKey, normalized, StringComparison.Ordinal))
                return;

            if (IsReservedModelKey(normalized))
                throw new ArgumentException($"'{normalized}' is reserved by the UsefulDoor central schematic.", nameof(value));

            if (_isSetup && !string.IsNullOrWhiteSpace(_customModelKey))
                SetBlockSpawned(_customModelKey, false, HiddenModelOffset);

            _customModelKey = normalized;
            ApplyModelVisibility();
        }
    }

    public KeycardPermissions KeycardPermissions { get; set; } = KeycardPermissions.None;
    public bool RequireAllPermissions { get; set; } = true;
    public bool CanClose { get; set; }
    public bool OneWay
    {
        get => !CanClose;
        set => CanClose = !value;
    }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            UpdateInteractable();
        }
    }

    private bool _enabled = true;

    public bool Locked
    {
        get => _locked;
        set
        {
            if (_locked == value)
                return;

            _locked = value;
            UpdateInteractable();
            if (_isSetup)
                PublishLinkedButtonState(_locked ? UDoorButtonState.Locked : GetStableButtonState());
        }
    }

    private bool _locked;
    public bool IsLocked
    {
        get => Locked;
        set => Locked = value;
    }

    public UDoorActionPolicy ActionPolicy { get; set; } = UDoorActionPolicy.Toggle;
    public UDoorActionPolicy Action
    {
        get => ActionPolicy;
        set => ActionPolicy = value;
    }

    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            if (_isOpen == value)
                return;

            _isOpen = value;
            if (_isSetup)
                ApplyVisualState(value, playAction: true);
        }
    }

    /// <summary>Maximum successful state changes. Zero or less means unlimited.</summary>
    public int MaxSuccessfulInteractions { get; set; }

    /// <summary>Runtime-only successful interaction count.</summary>
    public int SuccessfulInteractionCount { get; private set; }

    /// <summary>Open/Close animation is running and further interactions are ignored.</summary>
    public bool IsTransitioning { get; private set; }

    public string ClosedIdleAnimation { get; set; } = "idle";
    public string OpenAnimation { get; set; } = "opening";
    public string OpenIdleAnimation { get; set; } = "open";
    public string CloseAnimation { get; set; } = "closing";

    public string OpenAudio { get; set; } = "DoorOpen2.ogg";
    public string CloseAudio { get; set; } = "DoorClose2.ogg";
    public string FailAudio { get; set; } = string.Empty;
    public string IdleAudio { get; set; } = string.Empty;
    public string OpenSound
    {
        get => OpenAudio;
        set => OpenAudio = value;
    }
    public string CloseSound
    {
        get => CloseAudio;
        set => CloseAudio = value;
    }
    public string IdleLoopAudio
    {
        get => IdleAudio;
        set => IdleAudio = value;
    }

    public bool AudioSpatial { get; set; } = true;
    public float AudioVolume { get; set; } = 1f;
    public float AudioMaxDistance { get; set; } = 10f;
    public float AudioMinDistance { get; set; } = 1f;
    /// <summary>対象Animatorを取得できない場合に使用する遷移時間。</summary>
    public float ButtonStateTransitionDuration { get; set; } = 1f;

    /// <summary>
    /// Applies the complete Alpha profile first, then overlays settings defined by the current model type.
    /// </summary>
    public void ApplyDefaultsForModelType()
    {
        ApplyAlphaDefaults();

        switch (ModelType)
        {
            case UDoorModelType.Alpha:
                break;
            case UDoorModelType.Gate:
                OpenAudio = "BigDoorOpen.ogg";
                CloseAudio = "BigDoorClose.ogg";
                break;
            case UDoorModelType.Custom:
                break;
            default:
                Log.Warn($"[UsefulDoor] No model-specific default overlay is defined for '{ModelType}'; Alpha defaults are used.");
                break;
        }
    }

    private void ApplyAlphaDefaults()
    {
        ClosedIdleAnimation = "idle";
        OpenAnimation = "opening";
        OpenIdleAnimation = "open";
        CloseAnimation = "closing";
        OpenAudio = "DoorOpen2.ogg";
        CloseAudio = "DoorClose2.ogg";
        FailAudio = string.Empty;
        IdleAudio = string.Empty;
        AudioSpatial = true;
        AudioVolume = 1f;
        AudioMaxDistance = 10f;
        AudioMinDistance = 0.1f;
        ButtonStateTransitionDuration = 1f;
    }

    public override void ApplyOptions(Dictionary<string, string> options)
    {
        if (options == null || options.Count == 0)
            return;

        var remaining = new Dictionary<string, string>(options, StringComparer.OrdinalIgnoreCase);
        if (remaining.Remove(nameof(UseModelTypeDefaults), out string useDefaultsRaw) &&
            TryDeserializeOptionValue(useDefaultsRaw, typeof(bool), out object useDefaultsValue))
        {
            UseModelTypeDefaults = (bool)useDefaultsValue;
        }

        if (remaining.Remove(nameof(ModelType), out string modelTypeRaw) &&
            TryDeserializeOptionValue(modelTypeRaw, typeof(UDoorModelType), out object modelTypeValue))
        {
            ModelType = (UDoorModelType)modelTypeValue;
        }

        base.ApplyOptions(remaining);

        if (_isSetup)
            ApplyVisualState(_isOpen, playAction: false);
    }

    protected override void OnSetup()
    {
        _isSetup = true;
        SetupInteractable();
        ApplyModelVisibility();
        ApplyVisualState(_isOpen, playAction: false);
    }

    /// <summary>Attempts an interaction using the door's configured action policy.</summary>
    public UDoorInteractionResult TryInteract(Player? player = null, UDoorInteractionSource source = UDoorInteractionSource.Direct)
        => TryInteract(new UDoorInteractionContext(this, player, source));

    /// <summary>Attempts an interaction using a mutable interaction context.</summary>
    public UDoorInteractionResult TryInteract(UDoorInteractionContext context)
    {
        if (context == null || !ReferenceEquals(context.Door, this))
            return UDoorInteractionResult.InvalidContext;

        context.Result = UDoorInteractionResult.NoChange;
        bool beforeRaised = UDoor.RaiseBefore(context);
        if (!beforeRaised)
            return Finish(context, UDoorInteractionResult.Cancelled);

        if (!Enabled)
            return Finish(context, UDoorInteractionResult.Disabled);
        if (Locked)
            return Finish(context, UDoorInteractionResult.Locked);
        if (IsTransitioning)
            return Finish(context, UDoorInteractionResult.Transitioning);
        if (MaxSuccessfulInteractions > 0 && SuccessfulInteractionCount >= MaxSuccessfulInteractions)
            return Finish(context, UDoorInteractionResult.LimitReached);

        UDoorAction action = ResolveAction(context.RequestedAction);
        if (action != UDoorAction.Open && action != UDoorAction.Close)
            return Finish(context, UDoorInteractionResult.Failed);
        if (action == UDoorAction.Open && IsOpen)
            return Finish(context, UDoorInteractionResult.AlreadyOpen);
        if (action == UDoorAction.Close && !IsOpen)
            return Finish(context, UDoorInteractionResult.AlreadyClosed);
        if (action == UDoorAction.Close && !CanClose)
            return Finish(context, UDoorInteractionResult.OneWay);

        if (action == UDoorAction.Open && KeycardPermissions != KeycardPermissions.None &&
            (context.Player == null || !context.Player.HasPermission(KeycardPermissions, RequireAllPermissions)))
        {
            context.Player?.PlayKeycardInteractSound(false);
            return Finish(context, UDoorInteractionResult.PermissionDenied);
        }

        context.Player?.PlayKeycardInteractSound(true);
        bool nextOpen = action == UDoorAction.Open;
        _isOpen = nextOpen;
        SuccessfulInteractionCount++;
        ApplyVisualState(nextOpen, playAction: true);
        return Finish(context, UDoorInteractionResult.Success);
    }

    private UDoorAction ResolveAction(UDoorAction requested)
    {
        if (requested != UDoorAction.Toggle)
            return requested;

        return ActionPolicy switch
        {
            UDoorActionPolicy.Open => UDoorAction.Open,
            UDoorActionPolicy.Close => UDoorAction.Close,
            _ => IsOpen ? UDoorAction.Close : UDoorAction.Open,
        };
    }

    private UDoorInteractionResult Finish(UDoorInteractionContext context, UDoorInteractionResult result)
    {
        context.Result = result;
        if (result != UDoorInteractionResult.Success)
            PlayActionAudio(FailAudio);

        UDoor.RaiseAfter(context);
        return result;
    }

    private void SetupInteractable()
    {
        SelectModelInteractable();
        UpdateInteractable();
    }

    private void SelectModelInteractable()
    {
        InteractableHandle? selected = GetInteractableInBlock(GetSelectedModelKey(), InteractableKey) ??
                                       GetStandaloneInteractable(InteractableKey);
        bool createdFallback = false;
        if (selected == null)
        {
            if (_fallbackInteractable == null)
            {
                _fallbackInteractable = AddInteractable(
                    duration: 0.1f,
                    offset: InteractableLocalOffset,
                    scale: InteractableBaseScale);
                createdFallback = true;
            }

            selected = _fallbackInteractable;
        }

        foreach (InteractableHandle handle in Interactables)
        {
            handle.Enabled = false;
            DisableInteractableServerCollision(
                handle,
                retryAfterSpawn: createdFallback && ReferenceEquals(handle, _fallbackInteractable));
        }

        if (!ReferenceEquals(_interactable, selected))
        {
            if (_interactable != null)
            {
                _interactable.Interacting -= OnInteracting;
                _interactable.Interacted -= OnInteracted;
            }

            _interactable = selected;
            _interactable.Interacting += OnInteracting;
            _interactable.Interacted += OnInteracted;
        }

        UpdateInteractable();
    }

    private void OnInteracting(Player player, PlayerSearchingToyEventArgs eventArgs)
    {
        if (!Enabled || Locked)
            eventArgs.IsAllowed = false;
    }

    private void OnInteracted(Player player, PlayerSearchedToyEventArgs eventArgs)
    {
        TryInteract(new UDoorInteractionContext(this, player, UDoorInteractionSource.Direct, UDoorAction.Toggle, null, eventArgs));
    }

    private void UpdateInteractable()
    {
        if (_interactable != null)
            _interactable.Enabled = Enabled && !Locked;
    }

    private void ApplyVisualState(bool isOpen, bool playAction)
    {
        string animation = isOpen
            ? playAction ? OpenAnimation : OpenIdleAnimation
            : playAction ? CloseAnimation : ClosedIdleAnimation;
        Animator? animator = PlayAnimation(animation);
        UpdateLinkedButtonState(isOpen, playAction, animator, animation);

        if (playAction)
            PlayActionAudio(isOpen ? OpenAudio : CloseAudio);

        RestartIdleAudio();
    }

    private void UpdateLinkedButtonState(bool isOpen, bool transitioning, Animator? animator, string stateName)
    {
        int revision = ++_buttonStateRevision;
        if (Locked)
        {
            IsTransitioning = false;
            PublishLinkedButtonState(UDoorButtonState.Locked);
            return;
        }

        if (!transitioning)
        {
            IsTransitioning = false;
            PublishLinkedButtonState(isOpen ? UDoorButtonState.Open : UDoorButtonState.Close);
            return;
        }

        IsTransitioning = true;
        PublishLinkedButtonState(isOpen ? UDoorButtonState.Opening : UDoorButtonState.Closing);
        ScheduleAfterAnimatorState(animator, stateName, ButtonStateTransitionDuration, () =>
        {
            if (revision != _buttonStateRevision)
                return;

            IsTransitioning = false;
            PublishLinkedButtonState(Locked ? UDoorButtonState.Locked : GetStableButtonState());
        });
    }

    private UDoorButtonState GetStableButtonState()
        => IsOpen ? UDoorButtonState.Open : UDoorButtonState.Close;

    private void PublishLinkedButtonState(UDoorButtonState state)
        => UDoor.NotifyLinkedButtons(this, state);

    private Animator? PlayAnimation(string? animation)
    {
        Animator? animator = GetModelAnimator();
        if (string.IsNullOrWhiteSpace(animation) || animator == null)
            return animator;

        try
        {
            animator.Play(animation.Trim());
        }
        catch (Exception e)
        {
            Log.Warn($"[UsefulDoor] Failed to play animation '{animation}': {e.Message}");
        }

        return animator;
    }

    private Animator? GetModelAnimator()
    {
        if (Schematic == null)
            return null;

        SchematicBlock? modelRoot = GetBlock(GetSelectedModelKey());
        if (modelRoot == null)
            return null;

        Transform root = modelRoot.transform;
        return Schematic.AnimationController.Animators.FirstOrDefault(animator =>
            animator != null &&
            (animator.transform == root || animator.transform.IsChildOf(root)));
    }

    private void PlayActionAudio(string? audio)
    {
        if (string.IsNullOrWhiteSpace(audio))
            return;

        try
        {
            SpeakerApi.Play(
                audio.Trim(),
                $"udoor_{ObjectInstanceID}_action",
                Schematic?.Position ?? Position,
                destroyOnEnd: true,
                isSpatial: AudioSpatial,
                maxDistance: AudioMaxDistance,
                minDistance: AudioMinDistance,
                volume: AudioVolume);
        }
        catch (Exception e)
        {
            Log.Warn($"[UsefulDoor] Failed to play audio '{audio}': {e.Message}");
        }
    }

    private void RestartIdleAudio()
    {
        StopIdleAudio();
        if (!_isSetup || string.IsNullOrWhiteSpace(IdleAudio))
            return;

        try
        {
            _idlePlayback = SpeakerApi.PlayLoop(
                IdleAudio.Trim(),
                $"udoor_{ObjectInstanceID}_idle",
                Schematic?.Position ?? Position,
                isSpatial: AudioSpatial,
                maxDistance: AudioMaxDistance,
                minDistance: AudioMinDistance,
                volume: AudioVolume);
        }
        catch (Exception e)
        {
            Log.Warn($"[UsefulDoor] Failed to play idle audio '{IdleAudio}': {e.Message}");
        }
    }

    private void StopIdleAudio()
    {
        if (_idlePlayback.ControllerId != 0)
            _idlePlayback.Stop();

        _idlePlayback = default;
    }

    private void ApplyModelVisibility()
    {
        if (!_isSetup || Schematic == null)
            return;

        bool selectedFound = false;
        foreach (string key in ManagedBlockKeys)
        {
            bool isBuiltInModel = TryGetBuiltInModelType(key, out UDoorModelType blockType);
            bool isCustomModel = !string.IsNullOrWhiteSpace(CustomModelKey) &&
                                 string.Equals(key, CustomModelKey, StringComparison.OrdinalIgnoreCase);
            if (!isBuiltInModel && !isCustomModel)
                continue;

            bool selected = isBuiltInModel
                ? ModelType != UDoorModelType.Custom && ModelType == blockType
                : ModelType == UDoorModelType.Custom;
            SetBlockSpawned(key, selected, HiddenModelOffset);
            selectedFound |= selected;
        }

        if (!selectedFound)
            Log.Warn($"[UsefulDoor] No ObjectPrefabSchematicInfo block matched model '{GetSelectedModelKey()}'.");

        SelectModelInteractable();
    }

    private string GetSelectedModelKey()
        => ModelType == UDoorModelType.Custom ? CustomModelKey : ModelType.ToString();

    private static bool TryGetBuiltInModelType(string key, out UDoorModelType type)
        => Enum.TryParse(key, true, out type) &&
           Enum.IsDefined(typeof(UDoorModelType), type) &&
           type != UDoorModelType.Custom;

    private static bool IsReservedModelKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        if (string.Equals(key, InteractableKey, StringComparison.OrdinalIgnoreCase))
            return true;

        return TryGetBuiltInModelType(key, out _);
    }

    protected override void OnDestroy()
    {
        _buttonStateRevision++;
        IsTransitioning = false;
        StopIdleAudio();
        _interactable = null;
        _fallbackInteractable = null;
        _isSetup = false;
    }

    protected override void OnTransformUpdated()
    {
        base.OnTransformUpdated();
        if (_idlePlayback.IsValid)
            _idlePlayback.SetTransform(Schematic?.Position ?? Position);
    }
}
