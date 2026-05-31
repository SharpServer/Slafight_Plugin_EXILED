using System;
using System.Collections.Generic;
using System.Linq;
using AdminToys;
using Exiled.API.Enums;
using LabApi.Events.Arguments.PlayerEvents;
using MEC;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Player = Exiled.API.Features.Player;
using InteractableToy = LabApi.Features.Wrappers.InteractableToy;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

public class AuthDoor : ObjectPrefab
{
    public override float ToySearchRadius { get; set; } = 1.2f;

    private SchematicObject? _schematicObject;
    private InteractableToy? _interactableToy;
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
            SwitchDoor(value);
        }
    }
    private bool _isOpen = false;
    // ===== Lifecycle =====

    protected override void OnCreate()
    {
        _schematicObject = SpawnManagedSchematic("MovingDoor");

        Timing.CallDelayed(0.5f, () =>
        {
            CreateInteractableToy();
            // IsOpen=true → OpenIdle(door2) / IsOpen=false → CloseIdle(door0)
            _schematicObject?.AnimationController.Play(IsOpen ? "door2" : "door0");
        });
        base.OnCreate();
    }

    private void CreateInteractableToy()
    {
        _interactableToy = CreateManagedInteractable(
            interactionDuration: 0.1f,
            shape: InvisibleInteractableToy.ColliderShape.Box,
            localOffset: InteractableLocalOffset,
            baseScale: InteractableBaseScale);
    }

    protected override void OnDestroy()
    {
        _schematicObject = null;
        _interactableToy = null;
        base.OnDestroy();
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
MeowExtensions.ShowHint(            player, "<size=24>権限が足りないようだ</size>");
        }
    }

    private void SwitchDoor(bool isOpen)
    {
        SpeakerApi.Play(isOpen ? "ElevatorOpen1.ogg" : "ElevatorClose1.ogg", "KeycardDoorOpeningSound",
            _schematicObject?.Position ?? Position, true);

        // 開く: CloseToOpen(door1) → Animator側でOpenIdle(door2)に遷移
        // 閉じる: OpenToClose(door3) → Animator側でCloseIdle(door0)に遷移
        _schematicObject?.AnimationController.Play(isOpen ? "door1" : "door3");
    }

    // ===== Options (Save/Load) =====

    public override Dictionary<string, string> CollectOptions()
    {
        return new Dictionary<string, string>
        {
            ["KeycardPermissions"]    = KeycardPermissions.ToString(),
            ["RequireAllPermissions"] = RequireAllPermissions.ToString(),
            ["CanClose"]              = CanClose.ToString(),
            ["IsOpen"]                = IsOpen.ToString()
        };
    }

    public override void ApplyOptions(Dictionary<string, string> options)
    {
        if (options.TryGetValue("KeycardPermissions", out var kp)
            && Enum.TryParse<KeycardPermissions>(kp, true, out var perm))
        {
            KeycardPermissions = perm;
        }

        if (options.TryGetValue("RequireAllPermissions", out var rap)
            && bool.TryParse(rap, out var requireAll))
        {
            RequireAllPermissions = requireAll;
        }

        if (options.TryGetValue("CanClose", out var cc)
            && bool.TryParse(cc, out var canClose))
        {
            CanClose = canClose;
        }

        // IsOpenはOnCreate後のCallDelayed内でアニメーションが再生されるため、
        // バッキングフィールドを直接セットしてアニメーションはOnCreate側に任せる
        if (options.TryGetValue("IsOpen", out var io)
            && bool.TryParse(io, out var isOpen))
        {
            _isOpen = isOpen;
        }
    }

    // ===== Mod Command =====

    public override bool HandleModCommand(ArraySegment<string> args, out string response)
    {
        if (args.Count >= 2)
        {
            switch (args.At(1).ToLowerInvariant())
            {
                case "keycardpermissions":
                    if (args.Count < 3)
                    {
                        response = $"Current: {KeycardPermissions}\n" +
                                   $"Usage: mod keycardpermissions <perm1|perm2|...>\n" +
                                   $"Available: {string.Join(", ", Enum.GetNames(typeof(KeycardPermissions)))}";
                        return true;
                    }
                    var permRaw = string.Join("|", args.Skip(2)).Replace("|", ", ");
                    if (!Enum.TryParse<KeycardPermissions>(permRaw, true, out var newPerm))
                    {
                        response = $"Unknown permission '{permRaw}'.\n" +
                                   $"Available: {string.Join(", ", Enum.GetNames(typeof(KeycardPermissions)))}";
                        return true;
                    }
                    KeycardPermissions = newPerm;
                    response = $"Set KeycardPermissions to {newPerm}.";
                    return true;

                case "requireallpermissions":
                    if (args.Count < 3)
                    {
                        response = $"Current: {RequireAllPermissions}\nUsage: mod requireallpermissions <true|false>";
                        return true;
                    }
                    if (!bool.TryParse(args.At(2), out var requireAll))
                    {
                        response = $"Invalid value '{args.At(2)}'. Use true or false.";
                        return true;
                    }
                    RequireAllPermissions = requireAll;
                    response = $"Set RequireAllPermissions to {requireAll}.";
                    return true;

                case "canclose":
                    if (args.Count < 3)
                    {
                        response = $"Current: {CanClose}\nUsage: mod canclose <true|false>";
                        return true;
                    }
                    if (!bool.TryParse(args.At(2), out var canClose))
                    {
                        response = $"Invalid value '{args.At(2)}'. Use true or false.";
                        return true;
                    }
                    CanClose = canClose;
                    response = $"Set CanClose to {canClose}.";
                    return true;

                case "isopen":
                    if (args.Count < 3)
                    {
                        response = $"Current: {IsOpen}\nUsage: mod isopen <true|false>";
                        return true;
                    }
                    if (!bool.TryParse(args.At(2), out var isOpen))
                    {
                        response = $"Invalid value '{args.At(2)}'. Use true or false.";
                        return true;
                    }
                    IsOpen = isOpen;
                    response = $"Set IsOpen to {isOpen}.";
                    return true;
            }
        }

        return base.HandleModCommand(args, out response);
    }
}
