using Exiled.API.Enums;
using LabApi.Events.Arguments.PlayerEvents;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Player = Exiled.API.Features.Player;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

public class AuthDoor : ObjectPrefab
{
    protected override string SchematicName => "MovingDoor";

    private static readonly Vector3 InteractableLocalOffset = Vector3.up * 0.75f;
    private static readonly Vector3 InteractableBaseScale = Vector3.one + Vector3.up * 2f - new Vector3(-0.8f, 0f, -0.8f);

    public KeycardPermissions KeycardPermissions { get; set; } = KeycardPermissions.None;
    public bool RequireAllPermissions { get; set; } = true;

    /// <summary>
    /// trueの場合、開いた後に再度インタラクトで閉じることができる。
    /// falseの場合、一度開けたら閉じられない（door1のまま）。
    /// </summary>
    public bool CanClose { get; set; } = false;

    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            _isOpen = value;
            if (!string.IsNullOrEmpty(ObjectInstanceID))
                SwitchDoor(value);
        }
    }
    private bool _isOpen = false;
    // ===== Lifecycle =====

    protected override void OnSetup()
    {
        AddInteractable(duration: 0.1f, offset: InteractableLocalOffset, scale: InteractableBaseScale);

        // IsOpen=true → OpenIdle(door2) / IsOpen=false → CloseIdle(door0)
        Schematic?.AnimationController.Play(IsOpen ? "door2" : "door0");
    }

    // ===== Interaction =====

    protected override void OnToySearchedNearby(PlayerSearchedToyEventArgs ev)
    {
        var player = Player.Get(ev.Player);
        if (player == null)
            return;

        // 開いていて閉じられない場合はスキップ
        if (IsOpen && !CanClose) return;

        if (IsOpen)
        {
            // CanClose = true のとき、開いていたら閉じる（認証不要）
            player.PlayKeycardInteractSound(true);
            IsOpen = false;
            return;
        }

        // 閉じている場合は認証チェック
        if (player.HasPermission(KeycardPermissions, RequireAllPermissions))
        {
            player.PlayKeycardInteractSound(true);
            IsOpen = true;
        }
        else
        {
            player.PlayKeycardInteractSound(false);
            player.ShowHint("<size=24>権限が足りないようだ</size>");
        }
    }

    private void SwitchDoor(bool isOpen)
    {
        SpeakerApi.Play(isOpen ? "ElevatorOpen1.ogg" : "ElevatorClose1.ogg", "KeycardDoorOpeningSound",
            Schematic?.Position ?? Position, true);

        // 開く: CloseToOpen(door1) → Animator側でOpenIdle(door2)に遷移
        // 閉じる: OpenToClose(door3) → Animator側でCloseIdle(door0)に遷移
        Schematic?.AnimationController.Play(isOpen ? "door1" : "door3");
    }

    // KeycardPermissions / RequireAllPermissions / CanClose / IsOpen は
    // すべて public プロパティのため `.sl objprefab modify option <key> <value>` で汎用的に設定できる。
}
