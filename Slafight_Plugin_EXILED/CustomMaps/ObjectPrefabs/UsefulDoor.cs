using System;
using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using LabApi.Events.Arguments.PlayerEvents;
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

    private UDoorModelType _modelType = UDoorModelType.Alpha;
    private bool _useModelTypeDefaults = true;
    private string _customModelKey = string.Empty;
    private bool _isOpen;
    private bool _isSetup;
    private int _buttonStateRevision;
    private int _successfulInteractionCount;
    private InteractableHandle? _interactable;
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
    public bool UseModelTypeDefaults
    {
        get => _useModelTypeDefaults;
        set => _useModelTypeDefaults = value;
    }

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
                SetBlockSpawned(_customModelKey, false);

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
    public int SuccessfulInteractionCount => _successfulInteractionCount;

    public string ClosedIdleAnimation { get; set; } = "idle";
    public string OpenAnimation { get; set; } = "opening";
    public string OpenIdleAnimation { get; set; } = "open";
    public string CloseAnimation { get; set; } = "closing";

    public string OpenAudio { get; set; } = "DoorOpen2.ogg";
    public string CloseAudio { get; set; } = "DoorClose2.ogg";
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
        if (MaxSuccessfulInteractions > 0 && _successfulInteractionCount >= MaxSuccessfulInteractions)
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
        _successfulInteractionCount++;
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

    private static UDoorInteractionResult Finish(UDoorInteractionContext context, UDoorInteractionResult result)
    {
        context.Result = result;
        UDoor.RaiseAfter(context);
        return result;
    }

    private void SetupInteractable()
    {
        _interactable = GetInteractable(InteractableKey) ??
                        AddInteractable(duration: 0.1f, offset: InteractableLocalOffset, scale: InteractableBaseScale);
        _interactable.Interacting += OnInteracting;
        _interactable.Interacted += OnInteracted;
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
        UpdateLinkedButtonState(isOpen, playAction);

        string animation = isOpen
            ? playAction ? OpenAnimation : OpenIdleAnimation
            : playAction ? CloseAnimation : ClosedIdleAnimation;
        PlayAnimation(animation);

        if (playAction)
            PlayActionAudio(isOpen ? OpenAudio : CloseAudio);

        RestartIdleAudio();
    }

    private void UpdateLinkedButtonState(bool isOpen, bool transitioning)
    {
        int revision = ++_buttonStateRevision;
        if (Locked)
        {
            PublishLinkedButtonState(UDoorButtonState.Locked);
            return;
        }

        if (!transitioning)
        {
            PublishLinkedButtonState(isOpen ? UDoorButtonState.Open : UDoorButtonState.Close);
            return;
        }

        PublishLinkedButtonState(isOpen ? UDoorButtonState.Opening : UDoorButtonState.Closing);
        if (ButtonStateTransitionDuration <= 0f)
        {
            PublishLinkedButtonState(isOpen ? UDoorButtonState.Open : UDoorButtonState.Close);
            return;
        }

        ScheduleDelayed(ButtonStateTransitionDuration, () =>
        {
            if (revision != _buttonStateRevision)
                return;

            PublishLinkedButtonState(Locked ? UDoorButtonState.Locked : GetStableButtonState());
        });
    }

    private UDoorButtonState GetStableButtonState()
        => IsOpen ? UDoorButtonState.Open : UDoorButtonState.Close;

    private void PublishLinkedButtonState(UDoorButtonState state)
        => UDoor.NotifyLinkedButtons(this, state);

    private void PlayAnimation(string? animation)
    {
        if (string.IsNullOrWhiteSpace(animation) || Schematic == null)
            return;

        try
        {
            Schematic.AnimationController.Play(animation.Trim());
        }
        catch (Exception e)
        {
            Log.Warn($"[UsefulDoor] Failed to play animation '{animation}': {e.Message}");
        }
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
            SetBlockSpawned(key, selected);
            selectedFound |= selected;
        }

        if (!selectedFound)
            Log.Warn($"[UsefulDoor] No ObjectPrefabSchematicInfo block matched model '{GetSelectedModelKey()}'.");
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
        StopIdleAudio();
        _interactable = null;
        _isSetup = false;
    }

    protected override void OnTransformUpdated()
    {
        base.OnTransformUpdated();
        if (_idlePlayback.IsValid)
            _idlePlayback.SetTransform(Schematic?.Position ?? Position);
    }
}
