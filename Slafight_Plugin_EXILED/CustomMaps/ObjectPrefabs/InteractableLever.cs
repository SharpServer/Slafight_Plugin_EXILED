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
        handle.Searching += OnLeverSearching;
        handle.Searched += OnLeverSearched;

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

    private void OnLeverSearching(Player player, PlayerSearchingToyEventArgs ev)
    {
        if (!CanInteract) ev.IsAllowed = false;
    }

    private void OnLeverSearched(Player player, PlayerSearchedToyEventArgs ev)
    {
        if (Schematic == null)
            return;

        bool previousIsOn = IsOn;
        IsOn = !IsOn;
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
