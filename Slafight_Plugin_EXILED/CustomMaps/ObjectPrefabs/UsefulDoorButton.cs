using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using LabApi.Events.Arguments.PlayerEvents;
using MEC;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;
using Player = Exiled.API.Features.Player;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

/// <summary>
/// A schematic button that dispatches one interaction context to doors sharing
/// <see cref="TargetTag"/>. Door permissions remain entirely owned by UsefulDoor.
/// </summary>
public class UsefulDoorButton : ObjectPrefab
{
    public const string CentralSchematicName = "UsefulDoorButton";
    public const string InteractableKey = "Interactable";
    private UDoorButtonModelType _modelType = UDoorButtonModelType.Standard;
    private bool _useModelTypeDefaults = true;
    private string _customModelKey = string.Empty;
    private string _targetTag = string.Empty;
    private bool _enabled = true;
    private bool _isSetup;
    private UDoorButtonState _state = UDoorButtonState.Close;
    private int _stateRevision;
    private int _successfulActivationCount;
    private InteractableHandle? _interactable;
    private CoroutineHandle _resetHandle;
    private SpeakerApi.Playback _idlePlayback;

    protected override string SchematicName => CentralSchematicName;

    public UDoorButtonModelType ModelType
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
                ResetToIdle();
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
                throw new ArgumentException($"'{normalized}' is reserved by the UsefulDoorButton central schematic.", nameof(value));

            if (_isSetup && !string.IsNullOrWhiteSpace(_customModelKey))
                SetBlockSpawned(_customModelKey, false);

            _customModelKey = normalized;
            ApplyModelVisibility();
        }
    }

    /// <summary>Exact tag used to find UsefulDoor targets. Empty uses this button's Tag.</summary>
    public string TargetTag
    {
        get => _targetTag;
        set
        {
            _targetTag = value?.Trim() ?? string.Empty;
            if (_isSetup)
                SynchronizeStateFromTargets();
        }
    }

    public UDoorButtonTargetMode TargetMode { get; set; } = UDoorButtonTargetMode.All;
    public UDoorAction DoorAction { get; set; } = UDoorAction.Toggle;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            UpdateInteractable();
        }
    }

    /// <summary>Maximum successful button activations. Zero or less means unlimited.</summary>
    public int MaxSuccessfulButtonActivations { get; set; }
    public int MaxSuccessfulInteractions
    {
        get => MaxSuccessfulButtonActivations;
        set => MaxSuccessfulButtonActivations = value;
    }

    /// <summary>Runtime-only successful button activation count.</summary>
    public int SuccessfulActivationCount => _successfulActivationCount;

    /// <summary>Currently visible child state inside the selected model parent.</summary>
    public UDoorButtonState State => _state;

    public string SuccessAnimation { get; set; } = string.Empty;
    public string FailAnimation { get; set; } = string.Empty;
    public string IdleAnimation { get; set; } = string.Empty;
    public float ResetAnimationDelay { get; set; } = 0.25f;

    public string SuccessAudio { get; set; } = string.Empty;
    public string FailAudio { get; set; } = string.Empty;
    public string IdleAudio { get; set; } = string.Empty;
    public bool AudioSpatial { get; set; } = true;
    public float AudioVolume { get; set; } = 1f;
    public float AudioMaxDistance { get; set; } = 10f;
    public float AudioMinDistance { get; set; } = 1f;

    /// <summary>
    /// Applies the complete Standard profile first, then overlays settings defined by the current model type.
    /// </summary>
    public void ApplyDefaultsForModelType()
    {
        ApplyStandardDefaults();

        switch (ModelType)
        {
            case UDoorButtonModelType.Standard:
                break;
            case UDoorButtonModelType.Keycard:
                break;
            case UDoorButtonModelType.Custom:
                break;
            default:
                Log.Warn($"[UsefulDoorButton] No model-specific default overlay is defined for '{ModelType}'; Standard defaults are used.");
                break;
        }
    }

    private void ApplyStandardDefaults()
    {
        SuccessAnimation = string.Empty;
        FailAnimation = string.Empty;
        IdleAnimation = string.Empty;
        ResetAnimationDelay = 0.25f;
        SuccessAudio = string.Empty;
        FailAudio = string.Empty;
        IdleAudio = string.Empty;
        AudioSpatial = true;
        AudioVolume = 1f;
        AudioMaxDistance = 10f;
        AudioMinDistance = 1f;
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
            TryDeserializeOptionValue(modelTypeRaw, typeof(UDoorButtonModelType), out object modelTypeValue))
        {
            ModelType = (UDoorButtonModelType)modelTypeValue;
        }

        base.ApplyOptions(remaining);

        if (_isSetup)
            ResetToIdle();
    }

    protected override void OnSetup()
    {
        _isSetup = true;
        SetupInteractable();
        ApplyModelVisibility();
        SynchronizeStateFromTargets();
        ResetToIdle();
    }

    public UDoorInteractionResult TryInteract(Player? player = null)
        => TryInteract(player, null);

    private UDoorInteractionResult TryInteract(Player? player, PlayerSearchedToyEventArgs? eventArgs)
    {
        if (!Enabled)
            return UDoorInteractionResult.Disabled;
        if (MaxSuccessfulButtonActivations > 0 && _successfulActivationCount >= MaxSuccessfulButtonActivations)
            return UDoorInteractionResult.LimitReached;

        string tag = string.IsNullOrWhiteSpace(TargetTag) ? Tag : TargetTag;
        UsefulDoor[] targets = UDoor.GetDoors(tag).ToArray();
        if (targets.Length == 0)
        {
            PlayFeedback(false);
            return UDoorInteractionResult.NoTarget;
        }

        UDoorInteractionResult firstFailure = UDoorInteractionResult.Failed;
        bool succeeded = false;
        foreach (UsefulDoor target in targets)
        {
            if (target == null)
                continue;

            UDoorInteractionResult result = target.TryInteract(new UDoorInteractionContext(
                target,
                player,
                UDoorInteractionSource.Button,
                DoorAction,
                this,
                eventArgs));
            if (result == UDoorInteractionResult.Success)
            {
                succeeded = true;
                if (TargetMode == UDoorButtonTargetMode.First)
                    break;
            }
            else if (firstFailure == UDoorInteractionResult.Failed)
            {
                firstFailure = result;
            }

            if (TargetMode == UDoorButtonTargetMode.First)
                break;
        }

        if (!succeeded)
        {
            if (firstFailure is UDoorInteractionResult.Locked or UDoorInteractionResult.PermissionDenied)
                ShowLockedFeedback();

            PlayFeedback(false);
            return firstFailure;
        }

        _successfulActivationCount++;
        PlayFeedback(true);
        return UDoorInteractionResult.Success;
    }

    internal bool TargetsTag(string tag)
        => !string.IsNullOrWhiteSpace(tag) &&
           string.Equals(GetEffectiveTargetTag(), tag.Trim(), StringComparison.OrdinalIgnoreCase);

    public void SetDoorState(UDoorButtonState state)
    {
        _stateRevision++;
        _state = state;
        ApplyStateVisibility();
    }

    public void SynchronizeStateFromTargets()
    {
        UsefulDoor[] targets = UDoor.GetDoors(GetEffectiveTargetTag()).ToArray();
        if (targets.Length == 0)
            return;

        UDoorButtonState state = targets.Any(door => door.Locked)
            ? UDoorButtonState.Locked
            : targets.All(door => door.IsOpen)
                ? UDoorButtonState.Open
                : UDoorButtonState.Close;
        SetDoorState(state);
    }

    private string GetEffectiveTargetTag()
        => string.IsNullOrWhiteSpace(TargetTag) ? Tag : TargetTag;

    private void ShowLockedFeedback()
    {
        int revision = ++_stateRevision;
        _state = UDoorButtonState.Locked;
        ApplyStateVisibility();

        if (ResetAnimationDelay <= 0f)
        {
            SynchronizeStateFromTargets();
            return;
        }

        ScheduleDelayed(ResetAnimationDelay, () =>
        {
            if (revision == _stateRevision)
                SynchronizeStateFromTargets();
        });
    }

    private void SetupInteractable()
    {
        _interactable = GetInteractable(InteractableKey) ??
                        AddInteractable(duration: 0.1f, scale: Vector3.one * 0.75f);
        _interactable.Searching += OnSearching;
        _interactable.Searched += OnSearched;
        UpdateInteractable();
    }

    private void OnSearching(Player player, PlayerSearchingToyEventArgs eventArgs)
    {
        if (!Enabled)
            eventArgs.IsAllowed = false;
    }

    private void OnSearched(Player player, PlayerSearchedToyEventArgs eventArgs)
        => TryInteract(player, eventArgs);

    private void UpdateInteractable()
    {
        if (_interactable != null)
            _interactable.Enabled = Enabled;
    }

    private void PlayFeedback(bool success)
    {
        StopIdleAudio();
        PlayAnimation(success ? SuccessAnimation : FailAnimation);
        PlayAudio(success ? SuccessAudio : FailAudio, success ? "success" : "fail");

        if (!_isSetup)
            return;

        if (_resetHandle.IsRunning)
            Timing.KillCoroutines(_resetHandle);

        if (ResetAnimationDelay <= 0f)
            ResetToIdle();
        else
            _resetHandle = ScheduleDelayed(ResetAnimationDelay, ResetToIdle);
    }

    private void ResetToIdle()
    {
        PlayAnimation(IdleAnimation);
        RestartIdleAudio();
    }

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
            Log.Warn($"[UsefulDoorButton] Failed to play animation '{animation}': {e.Message}");
        }
    }

    private void PlayAudio(string? audio, string suffix)
    {
        if (string.IsNullOrWhiteSpace(audio))
            return;

        try
        {
            SpeakerApi.Play(
                audio.Trim(),
                $"udoorButton_{ObjectInstanceID}_{suffix}",
                Schematic?.Position ?? Position,
                destroyOnEnd: true,
                isSpatial: AudioSpatial,
                maxDistance: AudioMaxDistance,
                minDistance: AudioMinDistance,
                volume: AudioVolume);
        }
        catch (Exception e)
        {
            Log.Warn($"[UsefulDoorButton] Failed to play audio '{audio}': {e.Message}");
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
                $"udoorButton_{ObjectInstanceID}_idle",
                Schematic?.Position ?? Position,
                isSpatial: AudioSpatial,
                maxDistance: AudioMaxDistance,
                minDistance: AudioMinDistance,
                volume: AudioVolume);
        }
        catch (Exception e)
        {
            Log.Warn($"[UsefulDoorButton] Failed to play idle audio '{IdleAudio}': {e.Message}");
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
            bool isBuiltInModel = TryGetBuiltInModelType(key, out UDoorButtonModelType blockType);
            bool isCustomModel = !string.IsNullOrWhiteSpace(CustomModelKey) &&
                                 string.Equals(key, CustomModelKey, StringComparison.OrdinalIgnoreCase);
            if (!isBuiltInModel && !isCustomModel)
                continue;

            bool selected = isBuiltInModel
                ? ModelType != UDoorButtonModelType.Custom && ModelType == blockType
                : ModelType == UDoorButtonModelType.Custom;
            SetBlockSpawned(key, selected);
            selectedFound |= selected;
        }

        if (!selectedFound)
            Log.Warn($"[UsefulDoorButton] No ObjectPrefabSchematicInfo block matched model '{GetSelectedModelKey()}'.");

        ApplyStateVisibility();
    }

    private void ApplyStateVisibility()
    {
        if (!_isSetup || Schematic == null)
            return;

        string parentKey = GetSelectedModelKey();
        if (string.IsNullOrWhiteSpace(parentKey))
            return;

        bool selectedFound = false;
        foreach (UDoorButtonState state in Enum.GetValues(typeof(UDoorButtonState)))
        {
            bool selected = State == state;
            bool found = SetChildBlockSpawned(parentKey, state.ToString(), selected);
            selectedFound |= selected && found;
        }

        if (!selectedFound)
        {
            Log.Warn(
                $"[UsefulDoorButton] Child block '{State}' was not found below model parent '{parentKey}'.");
        }
    }

    private string GetSelectedModelKey()
        => ModelType == UDoorButtonModelType.Custom ? CustomModelKey : ModelType.ToString();

    private static bool TryGetBuiltInModelType(string key, out UDoorButtonModelType type)
        => Enum.TryParse(key, true, out type) &&
           Enum.IsDefined(typeof(UDoorButtonModelType), type) &&
           type != UDoorButtonModelType.Custom;

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
        _stateRevision++;
        if (_resetHandle.IsRunning)
            Timing.KillCoroutines(_resetHandle);

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
