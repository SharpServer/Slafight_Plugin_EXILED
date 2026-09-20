using System.Collections.Generic;
using LabApi.Events.Arguments.PlayerEvents;
using MEC;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Player = Exiled.API.Features.Player;
using MCB3Prefab = Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs.MCB3;

namespace Slafight_Plugin_EXILED.CustomItems.Utils;

/// <summary>
/// MCB3を照準位置へ設置するMedkitベースのカスタムアイテム。
/// 1回目の使用で本人だけに見えるプレビューを開始し、2回目で設置を確定する。
/// </summary>
public sealed class MCB3 : CustomUsableItem
{
    private const string SchematicName = "MCB3";
    private const float PreviewOpacity = 0.35f;
    private const float PreviewUpdateInterval = 0.05f;
    private const float PlacementDistance = 2.75f;
    private const float FloorProbeHeight = 2.5f;
    private const float FloorProbeDistance = 5f;
    private const float PreviewPositionSmoothing = 0.35f;
    private const float PreviewRotationSmoothing = 0.4f;

    private static readonly int PlacementMask = LayerMask.GetMask("Default");
    private static readonly Vector3 ModelPositionOffset = Vector3.up * 0.5f;
    private static readonly Quaternion ModelRotationOffset = Quaternion.Euler(0f, 180f, 0f);

    private SchematicObject? _preview;
    private CoroutineHandle _previewCoroutine;
    private Vector3 _placementPosition;
    private Quaternion _placementRotation = Quaternion.identity;

    public override ItemType BaseType => ItemType.Medkit;

    public override string Name => "MCB3";

    public override string Description => "使用して設置位置を選び、もう一度使用するとMCB3を設置する。";

    protected override void OnUseStarting(PlayerUsingItemEventArgs ev)
    {
        ev.IsAllowed = false;

        Player? player = Owner;
        if (!player.IsSafePlayer())
            return;

        if (_preview == null)
        {
            BeginPlacement(player!);
            return;
        }

        ConfirmPlacement(player!);
    }

    protected override void OnDeselected(PlayerChangedItemEventArgs ev) => ClearPreview();

    protected override void OnDropStarting(PlayerDroppingItemEventArgs ev) => ClearPreview();

    protected override void OnTrackingStopped() => ClearPreview();

    private void BeginPlacement(Player player)
    {
        ResolvePlacement(player, out _placementPosition, out _placementRotation);

        _preview = ObjectSpawner.SpawnSchematicPreview(
            SchematicName,
            _placementPosition,
            _placementRotation,
            Vector3.one,
            PreviewOpacity);

        if (_preview == null)
        {
            player.ShowHint("<size=26>MCB3のプレビューを生成できませんでした。</size>", 3f);
            return;
        }

        _preview.SetCollidable(false);
        foreach (Collider collider in _preview.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        var visibility = new NetworkShowState
        {
            OwnerId = player.Id,
            ShowToOwner = true,
            SpectatorVisibility = SpectatorVisibility.Hide,
        };
        _preview.NetworkIdentities.InitShowState(visibility);

        player.ShowHint("<size=26>視点で設置位置を選択し、もう一度使用して確定してください。</size>", 4f);
        _previewCoroutine = Timing.RunCoroutine(FollowView(player, Serial));
    }

    private void ConfirmPlacement(Player player)
    {
        ClearPreview();

        var placed = new MCB3Prefab
        {
            Position = _placementPosition,
            Rotation = _placementRotation,
        };
        placed.Create();

        if (placed.Schematic == null)
        {
            placed.DestroyImmediately();
            player.ShowHint("<size=26>MCB3を設置できませんでした。</size>", 3f);
            return;
        }

        player.ShowHint("<size=26>MCB3を設置しました。</size>", 3f);
        Destroy();
    }

    private IEnumerator<float> FollowView(Player player, ushort serial)
    {
        while (_preview != null &&
               player.IsSafePlayer() &&
               Owner == player &&
               player.CurrentItem?.Serial == serial)
        {
            ResolvePlacement(player, out Vector3 targetPosition, out Quaternion targetRotation);
            _placementPosition = Vector3.Lerp(
                _placementPosition,
                targetPosition,
                PreviewPositionSmoothing);
            _placementRotation = Quaternion.Slerp(
                _placementRotation,
                targetRotation,
                PreviewRotationSmoothing);
            _preview.Position = _placementPosition;
            _preview.Rotation = _placementRotation;
            yield return Timing.WaitForSeconds(PreviewUpdateInterval);
        }

        ClearPreview();
    }

    private static void ResolvePlacement(Player player, out Vector3 position, out Quaternion rotation)
    {
        Transform camera = player.CameraTransform;
        Vector3 horizontalForward = Vector3.ProjectOnPlane(camera.forward, Vector3.up);
        if (horizontalForward.sqrMagnitude < 0.001f)
            horizontalForward = Vector3.ProjectOnPlane(player.Transform.forward, Vector3.up);

        horizontalForward.Normalize();
        Vector3 desiredPosition = player.Position + horizontalForward * PlacementDistance;
        Vector3 probeOrigin = desiredPosition + Vector3.up * FloorProbeHeight;

        if (PlacementMask != 0 && Physics.Raycast(
                probeOrigin,
                Vector3.down,
                out RaycastHit hit,
                FloorProbeDistance,
                PlacementMask,
                QueryTriggerInteraction.Ignore))
        {
            position = hit.point + Vector3.up * 0.015f;
        }
        else
        {
            // 床を取得できないフレームでも、視線Rayの遠方へ飛ばさず足元の高さを維持する。
            position = desiredPosition;
        }

        position += ModelPositionOffset;

        Vector3 towardPlayer = player.Position - position;
        towardPlayer.y = 0f;
        Quaternion facingPlayer = towardPlayer.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(towardPlayer.normalized, Vector3.up)
            : Quaternion.Euler(0f, player.Rotation.eulerAngles.y + 180f, 0f);
        rotation = facingPlayer * ModelRotationOffset;
    }

    private void ClearPreview()
    {
        if (_previewCoroutine.IsRunning)
            Timing.KillCoroutines(_previewCoroutine);

        _previewCoroutine = default;

        SchematicObject? preview = _preview;
        _preview = null;
        if (preview == null)
            return;

        preview.NetworkIdentities.RemoveShowState();
        preview.Destroy();
    }
}
