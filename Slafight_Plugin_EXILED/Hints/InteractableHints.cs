using System;
using System.Collections.Generic;
using AdminToys;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Utilities;
using Interactables;
using Interactables.Verification;
using LabApi.Events.Arguments.ServerEvents;
using MEC;
using ProjectMER.Features.Extensions;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Hint = HintServiceMeow.Core.Models.Hints.Hint;
using LabRoom = LabApi.Features.Wrappers.Room;

namespace Slafight_Plugin_EXILED.Hints;

public sealed class InteractableHints : EventHandlerBase
{
    private const string HintId = "Slafight_InteractablePrompt";
    private const float UpdateInterval = 0.1f;

    private readonly RaycastHit[] _rayHits = new RaycastHit[16];
    private readonly Dictionary<int, PlayerDisplay> _visibleHints = new();
    private CoroutineHandle _loop;

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Player.Left += OnLeft;
        _loop = Timing.RunCoroutine(UpdateHints());
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Player.Left -= OnLeft;
        Timing.KillCoroutines(_loop);
        ClearHints();
    }

    public override void OnServerRoundEnded(RoundEndedEventArgs ev) => ClearHints();
    public override void OnServerRoundRestarted() => ClearHints();

    private IEnumerator<float> UpdateHints()
    {
        while (true)
        {
            if (Round.IsStarted && !Round.IsEnded)
            {
                foreach (Player player in Player.List)
                {
                    if (!player.IsSafePlayer() || !NetGuards.IsReadyClient(player) || !player.IsAlive)
                    {
                        RemoveHint(player.Id);
                        continue;
                    }

                    var toy = FindLookTarget(player);
                    if (toy == null)
                    {
                        RemoveHint(player.Id);
                        continue;
                    }

                    ShowHint(player, GetDisplayName(toy));
                }
            }

            yield return Timing.WaitForSeconds(UpdateInterval);
        }
    }

    private InvisibleInteractableToy? FindLookTarget(Player player)
    {
        var camera = player.CameraTransform;
        if (camera == null)
            return null;

        var hub = player.ReferenceHub;
        var search = hub.searchCoordinator;
        float interactDistance = StandardDistanceVerification.DefaultMaxDistance;
        float searchDistance = search != null ? search.RayDistance : 0f;
        float distance = Mathf.Max(interactDistance, searchDistance);
        var ray = new Ray(camera.position, camera.forward);
        int mask = InteractionCoordinator.RaycastMask;
        RaycastHit[] hits = _rayHits;
        int count = Physics.RaycastNonAlloc(ray, hits, distance, mask, QueryTriggerInteraction.Collide);

        // A full buffer can omit the nearest hit.
        if (count == hits.Length)
        {
            hits = Physics.RaycastAll(ray, distance, mask, QueryTriggerInteraction.Collide);
            count = hits.Length;
        }

        InvisibleInteractableToy? target = null;
        float nearestDistance = float.PositiveInfinity;
        float obstructionDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];
            Collider collider = hit.collider;
            if (collider == null || collider.transform.IsChildOf(hub.transform))
                continue;

            if (collider.TryGetComponent(out InvisibleInteractableToy toy) && collider == toy._collider)
            {
                if (hit.distance < nearestDistance)
                {
                    target = toy;
                    nearestDistance = hit.distance;
                }
            }
            else if (!collider.isTrigger && hit.distance < obstructionDistance)
            {
                obstructionDistance = hit.distance;
            }
        }

        if (target == null || !target.isServer || target.IsLocked || nearestDistance > obstructionDistance ||
            (ObjectPrefabInteractionRouter.TryRouteToy(target, out var handle) && !handle.Enabled))
            return null;

        if (target.InteractionDuration <= 0f)
            return nearestDistance < interactDistance ? target : null;

        return search != null && nearestDistance <= searchDistance &&
            (target.transform.position - hub.transform.position).sqrMagnitude <= search.ServerMaxRayDistanceSqr
            ? target : null;
    }

    private static string GetDisplayName(InvisibleInteractableToy toy)
    {
        if (toy.TryGetComponent(out InteractableHintMetadata metadata) && metadata.DisplayName.Length != 0)
            return metadata.DisplayName;

        SchematicObject? schematic = toy.TryGetComponent(out SchematicBlock block)
            ? block.Schematic
            : toy.GetComponentInParent<SchematicObject>();
        if (schematic == null && ObjectPrefabInteractionRouter.TryRouteToy(toy, out var handle))
            schematic = handle.Owner.Schematic;
        if (schematic != null)
            return $"{schematic.Name} Interactable";

        RoomType roomType = LabRoom.GetRoomAtPosition(toy.transform.position) is { } room
            ? room.FindRoomType()
            : RoomType.Unknown;
        return $"{roomType} Interactable";
    }

    private void ShowHint(Player player, string name)
    {
        if (!_visibleHints.TryGetValue(player.Id, out var display))
        {
            display = PlayerDisplay.Get(player.ReferenceHub);
            _visibleHints.Add(player.Id, display);
        }

        string label = name.Replace('\r', ' ').Replace('\n', ' ').Replace('<', '＜').Replace('>', '＞');
        string text = $"<color=#C8C8C8>{label}\nインタラクト可能</color>";
        if (display.GetHint(HintId) is { } hint)
        {
            if (!string.Equals(hint.Text, text, StringComparison.Ordinal))
                hint.Text = text;
            return;
        }

        display.AddHint(new Hint
        {
            Id = HintId,
            Text = text,
            FontSize = 26,
            Alignment = HintAlignment.Center,
            YCoordinate = 600,
            SyncSpeed = HintSyncSpeed.Fast,
        });
    }

    private void OnLeft(LeftEventArgs ev) => RemoveHint(ev.Player.Id);

    private void RemoveHint(int playerId)
    {
        if (!_visibleHints.TryGetValue(playerId, out var display))
            return;

        _visibleHints.Remove(playerId);
        display.RemoveHint(HintId);
    }

    private void ClearHints()
    {
        foreach (var display in _visibleHints.Values)
            display.RemoveHint(HintId);
        _visibleHints.Clear();
    }
}
