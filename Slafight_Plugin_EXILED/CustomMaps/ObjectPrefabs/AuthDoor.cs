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
    private int _transitionRevision;

    public KeycardPermissions KeycardPermissions { get; set; } = KeycardPermissions.None;
    public bool RequireAllPermissions { get; set; } = true;

    /// <summary>
    /// trueの場合、開いた後に再度インタラクトで閉じることができる。
    /// falseの場合、一度開けたら閉じられない（door1のまま）。
    /// </summary>
    public bool CanClose { get; set; } = false;

    /// <summary>Animatorを取得できない場合に使用する遷移時間。</summary>
    public float TransitionDuration { get; set; } = 1f;

    public bool IsTransitioning { get; private set; }

    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            if (_isOpen == value)
                return;

            _isOpen = value;
            if (!string.IsNullOrEmpty(ObjectInstanceID))
                SwitchDoor(value);
        }
    }
    private bool _isOpen = false;
    // ===== Lifecycle =====

    protected override void OnSetup()
    {
        AddInteractable(duration: 0f, offset: InteractableLocalOffset, scale: InteractableBaseScale);

        // IsOpen=true → OpenIdle(door2) / IsOpen=false → CloseIdle(door0)
        GetAnimator()?.Play(IsOpen ? "door2" : "door0");
    }

    // ===== Interaction =====

    protected override void OnToyInteractedNearby(PlayerSearchedToyEventArgs ev)
    {
        var player = Player.Get(ev.Player);
        if (player == null)
            return;

        if (IsTransitioning)
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
        string stateName = isOpen ? "door1" : "door3";
        Animator? animator = GetAnimator();
        animator?.Play(stateName);
        BeginTransition(animator, stateName);

        SpeakerApi.Play(isOpen ? "ElevatorOpen1.ogg" : "ElevatorClose1.ogg", "KeycardDoorOpeningSound",
            Schematic?.Position ?? Position, true);
    }

    private Animator? GetAnimator()
    {
        var animators = Schematic?.AnimationController.Animators;
        return animators is { Count: 1 } ? animators[0] : null;
    }

    private void BeginTransition(Animator? animator, string stateName)
    {
        int revision = ++_transitionRevision;
        IsTransitioning = true;
        ScheduleAfterAnimatorState(animator, stateName, TransitionDuration, () =>
        {
            if (revision == _transitionRevision)
                IsTransitioning = false;
        });
    }

    protected override void OnDestroy()
    {
        _transitionRevision++;
        IsTransitioning = false;
    }

    // KeycardPermissions / RequireAllPermissions / CanClose / IsOpen は
    // すべて public プロパティのため `.sl objprefab modify option <key> <value>` で汎用的に設定できる。
}
