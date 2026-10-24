using System;
using System.Collections.Generic;
using AdminToys;
using Exiled.API.Features;
using LabApi.Events.Arguments.PlayerEvents;
using MEC;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

/// <summary>Behavior bound to an authored block; never owns the surrounding schematic.</summary>
public abstract class UsefulDoorObject : ObjectPrefab, IObjectPrefabMarkerReceiver
{
    protected SchematicObjectPrefabObject? Marker;
    private InteractableHandle? interaction;

    public int AuthoringId { get; set; }
    public Vector3 InteractionCenter { get; set; }
    public Vector3 InteractionSize { get; set; } = Vector3.one;
    public float InteractionDuration { get; set; }
    public bool InteractionEnabled { get; set; } = true;
    public bool IsLocked { get; set; }
    public override bool IsSaveable { get; set; } = false;
    protected override float SetupDelay => 0f;

    public void BindMarker(SchematicObjectPrefabObject marker) => Marker = marker;
    internal bool IsInSchematic(SchematicObject? schematic)
        => Marker != null && schematic != null && ReferenceEquals(Marker.Schematic, schematic);
    public override bool FollowMarkerTransform => false;

    protected override void OnSetup()
    {
        if (Marker == null || !InteractionEnabled)
            return;
        interaction = AddInteractable(duration: InteractionDuration, offset: InteractionCenter, scale: InteractionSize);
        interaction.Interacting += OnInteracting;
        interaction.Interacted += OnInteracted;
        SyncManagedObjects();
    }

    private void OnInteracting(Player player, PlayerSearchingToyEventArgs ev)
    {
        if (!player.IsSafePlayer() || IsLocked || Marker == null || !CanInteract)
            ev.IsAllowed = false;
    }

    private void OnInteracted(Player player, PlayerSearchedToyEventArgs ev)
    {
        if (player.IsSafePlayer() && !IsLocked && Marker != null && CanInteract)
            Interact(player);
    }

    protected virtual bool CanInteract => true;
    protected abstract void Interact(Player player);

    public override void SyncManagedObjects()
    {
        if (Marker == null)
            return;
        foreach (InteractableHandle handle in Interactables)
        {
            if (handle.Toy.IsDestroyed)
                continue;
            Transform root = Marker.transform;
            // Keep toys unparented: network coordinates are world coordinates.
            handle.Toy.Transform.SetParent(null, true);
            handle.Toy.Transform.SetPositionAndRotation(root.TransformPoint(InteractionCenter), root.rotation);
            // LossyScale alone drops shear caused by rotated, non-uniformly scaled parents.
            // Project all three transformed half-axes onto the toy's rotation basis.
            Quaternion inverse = Quaternion.Inverse(root.rotation);
            Vector3 x = inverse * root.TransformVector(Vector3.right * InteractionSize.x);
            Vector3 y = inverse * root.TransformVector(Vector3.up * InteractionSize.y);
            Vector3 z = inverse * root.TransformVector(Vector3.forward * InteractionSize.z);
            handle.Toy.Transform.localScale = new Vector3(
                Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            handle.Toy.Base.NetworkMovementSmoothing = 0;
            handle.Toy.Base.NetworkPosition = handle.Toy.Transform.position;
            handle.Toy.Base.NetworkRotation = handle.Toy.Transform.rotation;
            handle.Toy.Base.NetworkScale = handle.Toy.Transform.localScale;
        }
    }

    protected void ReleaseInteraction()
    {
        if (interaction != null)
        {
            interaction.Interacting -= OnInteracting;
            interaction.Interacted -= OnInteracted;
            interaction = null;
        }
        Marker = null;
    }

    protected override void OnDestroy() => ReleaseInteraction();
    protected override void OnRoundRestarting() => ReleaseInteraction();
}

/// <summary>A sliding or custom animated door authored using UDoorComponent in Unity.</summary>
public sealed class UsefulDoor : UsefulDoorObject
{
    private CoroutineHandle animation;
    private Vector3 closedPosition;
    private AdminToyBase[] movingToys = [];
    private Animator? overrideAnimator;
    private bool overrideAnimatorWasEnabled;
    private AnimatorCullingMode overrideAnimatorCulling;
    private bool overrideReady;

    public Vector3 SlideOffset { get; set; } = new(1.5f, 0f, 0f);
    public float AnimationDuration { get; set; } = 0.7f;
    public bool StartOpen { get; set; }
    public UDoorActionPolicy ActionPolicy { get; set; } = UDoorActionPolicy.Toggle;
    public int OverrideAnimatorId { get; set; }
    public string OpenAnimationState { get; set; } = "Open";
    public string CloseAnimationState { get; set; } = "Close";
    public int AnimationLayer { get; set; }
    public bool IsOpen { get; private set; }
    public bool IsTransitioning { get; private set; }
    protected override bool CanInteract => !IsTransitioning && (OverrideAnimatorId == 0 || overrideReady);

    protected override void OnSetup()
    {
        if (Marker != null)
        {
            closedPosition = Marker.transform.localPosition;
            movingToys = Marker.GetComponentsInChildren<AdminToyBase>(true);
            foreach (AdminToyBase toy in movingToys)
            {
                toy.NetworkIsStatic = false;
                toy.NetworkMovementSmoothing = 60;
            }
            IsOpen = StartOpen;
            if (OverrideAnimatorId != 0)
            {
                overrideAnimator = Marker.Schematic?.GetBlockById(OverrideAnimatorId)?.GetComponent<Animator>();
                if (overrideAnimator != null && overrideAnimator.runtimeAnimatorController != null)
                {
                    overrideAnimatorWasEnabled = overrideAnimator.enabled;
                    overrideAnimatorCulling = overrideAnimator.cullingMode;
                    // UDoor samples the requested state itself; avoid a second Animator clock.
                    overrideAnimator.enabled = false;
                    overrideAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    overrideAnimator.Rebind();
                    overrideAnimator.Update(0f);
                    overrideReady = AnimationLayer >= 0 && AnimationLayer < overrideAnimator.layerCount &&
                        overrideAnimator.HasState(AnimationLayer, Animator.StringToHash(OpenAnimationState)) &&
                        overrideAnimator.HasState(AnimationLayer, Animator.StringToHash(CloseAnimationState));
                }
                if (!overrideReady)
                    Log.Warn($"[UDoor] '{Marker.name}' の独自Animatorまたは開閉ステートが見つかりません。Layer={AnimationLayer}, Open='{OpenAnimationState}', Close='{CloseAnimationState}'");
                else SetAnimationProgress(IsOpen, 1f);
            }
            else SetProgress(IsOpen ? 1f : 0f);
        }
        base.OnSetup();
    }

    protected override void Interact(Player player) => Request(ActionPolicy switch
    {
        UDoorActionPolicy.Open => UDoorAction.Open,
        UDoorActionPolicy.Close => UDoorAction.Close,
        _ => UDoorAction.Toggle,
    });

    public UDoorInteractionResult Request(UDoorAction action = UDoorAction.Toggle)
    {
        if (Marker == null) return UDoorInteractionResult.InvalidContext;
        if (IsLocked) return UDoorInteractionResult.Locked;
        if (IsTransitioning) return UDoorInteractionResult.Transitioning;
        if (OverrideAnimatorId != 0 && (!overrideReady || overrideAnimator == null)) return UDoorInteractionResult.Failed;
        bool open = action == UDoorAction.Toggle ? !IsOpen : action == UDoorAction.Open;
        if (open == IsOpen)
            return open ? UDoorInteractionResult.AlreadyOpen : UDoorInteractionResult.AlreadyClosed;
        IsTransitioning = true;
        animation = Timing.RunCoroutine(Animate(open));
        return UDoorInteractionResult.Success;
    }

    private IEnumerator<float> Animate(bool open)
    {
        float duration = Mathf.Max(0f, AnimationDuration);
        float start = Time.time;
        while (Marker != null && duration > 0f && Time.time - start < duration)
        {
            SetAnimationProgress(open, (Time.time - start) / duration);
            yield return Timing.WaitForOneFrame;
        }
        if (Marker != null)
        {
            IsOpen = open;
            SetAnimationProgress(open, 1f);
        }
        IsTransitioning = false;
        animation = default;
    }

    private void SetProgress(float progress)
    {
        if (Marker == null) return;
        // SlideOffset is in the door's own axes, including authored scale/rotation.
        Marker.transform.localPosition = closedPosition +
            Marker.transform.localRotation * Vector3.Scale(Marker.transform.localScale, SlideOffset) * progress;
        SyncAnimationObjects();
    }

    private void SetAnimationProgress(bool open, float progress)
    {
        if (OverrideAnimatorId == 0)
        {
            float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
            SetProgress(open ? eased : 1f - eased);
            return;
        }
        if (overrideAnimator == null || !overrideReady) return;
        string state = open ? OpenAnimationState : CloseAnimationState;
        // Keep looping states just below the next cycle at the final pose.
        overrideAnimator.Play(Animator.StringToHash(state), AnimationLayer, Mathf.Min(Mathf.Clamp01(progress), 0.999999f));
        overrideAnimator.Update(0f);
        SyncAnimationObjects();
    }

    private void SyncAnimationObjects()
    {
        foreach (AdminToyBase toy in movingToys)
        {
            if (toy == null) continue;
            toy.UpdatePositionServer();
        }
        SyncManagedObjects();
    }

    private void StopAnimation(bool restore)
    {
        if (animation.IsRunning) Timing.KillCoroutines(animation);
        animation = default;
        IsTransitioning = false;
        if (restore)
        {
            SetAnimationProgress(false, 1f);
            if (overrideAnimator != null)
            {
                overrideAnimator.cullingMode = overrideAnimatorCulling;
                overrideAnimator.enabled = overrideAnimatorWasEnabled;
            }
        }
        overrideAnimator = null;
        overrideReady = false;
        movingToys = [];
        ReleaseInteraction();
    }

    protected override void OnDestroy() => StopAnimation(restore: true);
    protected override void OnRoundRestarting() => StopAnimation(restore: false);
}
