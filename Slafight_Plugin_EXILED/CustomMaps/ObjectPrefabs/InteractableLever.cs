using System;
using Exiled.Events.EventArgs.Interfaces;
using LabApi.Events.Arguments.PlayerEvents;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;
using Player = Exiled.API.Features.Player;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

public class InteractableLeverTogglingEventArgs : EventArgs, IDeniableEvent, IPlayerEvent
{
    public InteractableLeverTogglingEventArgs(
        InteractableLever lever,
        Player player,
        PlayerSearchedToyEventArgs sourceEventArgs,
        bool previousIsOn,
        bool isOn,
        bool isAllowed = true)
    {
        Lever = lever;
        Player = player;
        SourceEventArgs = sourceEventArgs;
        PreviousIsOn = previousIsOn;
        IsOn = isOn;
        IsAllowed = isAllowed;
    }

    public InteractableLever Lever { get; }
    public Player Player { get; }
    public PlayerSearchedToyEventArgs SourceEventArgs { get; }
    public bool PreviousIsOn { get; }
    public bool IsOn { get; set; }
    public bool IsAllowed { get; set; }
    public bool TurnedOn => !PreviousIsOn && IsOn;
    public bool TurnedOff => PreviousIsOn && !IsOn;
    public string Tag => Lever.Tag;
}

public class InteractableLeverToggledEventArgs : EventArgs, IPlayerEvent
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
    public static event EventHandler<InteractableLeverTogglingEventArgs>? Toggling;
    public static event EventHandler<InteractableLeverToggledEventArgs>? Toggled;

    protected override string SchematicName => "InteractableLever";

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

    protected override void OnSetup()
    {
        if (Schematic == null) return;

        var handle = AddInteractable(duration: 0.05f, scale: Vector3.one * 0.25f);
        handle.Interacting += OnLeverInteracting;
        handle.Interacted += OnLeverInteracted;

        AnimationLever(IsOn);
    }

    private void AnimationLever(bool nextIsOn)
    {
        if (Schematic == null) return;
        string animState = nextIsOn switch
        {
            true => "Mode1",
            false => "Mode0"
        };
        Schematic.AnimationController.Play(animState);
    }

    private void OnLeverInteracting(Player player, PlayerSearchingToyEventArgs ev)
    {
        if (!CanInteract) ev.IsAllowed = false;
    }

    private void OnLeverInteracted(Player player, PlayerSearchedToyEventArgs ev)
    {
        if (Schematic == null)
            return;

        bool previousIsOn = IsOn;
        var togglingEventArgs = new InteractableLeverTogglingEventArgs(this, player, ev, previousIsOn, !previousIsOn, CanInteract);
        Toggling?.Invoke(this, togglingEventArgs);

        if (!togglingEventArgs.IsAllowed || togglingEventArgs.IsOn == previousIsOn)
            return;

        IsOn = togglingEventArgs.IsOn;
        SpeakerApi.Play("LeverFlip.ogg", GetSoundId(ev), Schematic.Position, true, isSpatial: false, maxDistance: 10f, minDistance: 0.1f);
        Toggled?.Invoke(this, new InteractableLeverToggledEventArgs(this, player, ev, previousIsOn, IsOn));
    }

    public string GetSoundId(PlayerSearchedToyEventArgs ev)
    {
        string id = ev.Interactable != null
            ? ev.Interactable.Base.netId.ToString()
            : ObjectInstanceID;

        return $"interactableLever_{id}";
    }
}
