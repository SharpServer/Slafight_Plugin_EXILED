using System;
using AdminToys;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;
using Player = Exiled.API.Features.Player;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

public class InteractableLeverToggledEventArgs : EventArgs
{
    public InteractableLeverToggledEventArgs(
        InteractableLever lever,
        Player player,
        PlayerSearchedToyEventArgs sourceEventArgs,
        bool previousIsOn,
        bool isOn)
    {
        Lever = lever;
        Player = player;
        SourceEventArgs = sourceEventArgs;
        PreviousIsOn = previousIsOn;
        IsOn = isOn;
    }

    public InteractableLever Lever { get; }
    public Player Player { get; }
    public PlayerSearchedToyEventArgs SourceEventArgs { get; }
    public bool PreviousIsOn { get; }
    public bool IsOn { get; }
    public bool TurnedOn => !PreviousIsOn && IsOn;
    public bool TurnedOff => PreviousIsOn && !IsOn;
    public string Tag => Lever.Tag;
}

public class InteractableLever : ObjectPrefab
{
    public static event EventHandler<InteractableLeverToggledEventArgs>? Toggled;

    public override float ToySearchRadius { get; set; } = 1.75f;
    public bool IsOn
    {
        get;
        set
        {
            field = value;
            AnimationLever(value);
        }
    } = false;

    public bool CanInteract { get; set; } = true;

    private SchematicObject? _schematicObject;
    private InteractableToy? _interactableToy;
    private static readonly Vector3 InteractableLocalOffset = Vector3.zero;
    private static readonly Vector3 InteractableBaseScale = Vector3.one * 0.25f;

    protected override void OnCreate()
    {
        _schematicObject = SpawnManagedSchematic("InteractableLever");
        ScheduleDelayed(0.5f, Setup);
        base.OnCreate();
    }

    private void Setup()
    {
        if (_schematicObject == null) return;

        _interactableToy = CreateManagedInteractable(
            interactionDuration: 0.05f,
            shape: InvisibleInteractableToy.ColliderShape.Box,
            localOffset: InteractableLocalOffset,
            baseScale: InteractableBaseScale);
        
        AnimationLever(IsOn);
    }

    private void AnimationLever(bool nextIsOn)
    {
        if (_schematicObject == null) return;
        string animState = nextIsOn switch
        {
            true => "Mode1",
            false => "Mode0"
        };
        _schematicObject.AnimationController.Play(animState);
    }

    protected override void OnDestroy()
    {
        _schematicObject = null;
        _interactableToy = null;
        base.OnDestroy();
    }

    protected override void OnToySearchingNearby(PlayerSearchingToyEventArgs eventArgs)
    {
        if (!CanInteract) eventArgs.IsAllowed = false;
        base.OnToySearchingNearby(eventArgs);
    }

    protected override void OnToySearchedNearby(PlayerSearchedToyEventArgs ev)
    {
        var player = Player.Get(ev.Player);
        if (player == null || _schematicObject == null)
            return;

        bool previousIsOn = IsOn;
        IsOn = !IsOn;
        SpeakerApi.Play("LeverFlip.ogg", GetSoundId(ev), _schematicObject.Position, true, isSpatial: false, maxDistance: 10f, minDistance: 0.1f);
        Toggled?.Invoke(this, new InteractableLeverToggledEventArgs(this, player, ev, previousIsOn, IsOn));
    }

    private string GetSoundId(PlayerSearchedToyEventArgs ev)
    {
        string id = ev.Interactable != null
            ? ev.Interactable.Base.netId.ToString()
            : ObjectInstanceID;

        return $"interactableLever_{id}";
    }
}
