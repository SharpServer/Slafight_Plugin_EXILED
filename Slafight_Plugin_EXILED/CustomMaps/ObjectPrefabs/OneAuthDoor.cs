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
using EventHandler = Slafight_Plugin_EXILED.MainHandlers.EventHandler;
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
    public bool IsOpen
    {
        get;
        set
        {
            field = value;
            SwitchDoor(value);
        }
    } = false;

    private static readonly Action<string, string, Vector3, bool, Transform, bool, float, float> CreateAndPlayAudio
        = EventHandler.CreateAndPlayAudio;

    // ===== Lifecycle =====

    protected override void OnCreate()
    {
        _schematicObject = SpawnManagedSchematic("MovingDoor");

        Timing.CallDelayed(0.5f, () =>
        {
            CreateInteractableToy();
            _schematicObject?.AnimationController.Play(IsOpen ? "door1" : "door3");
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

        if (IsOpen) return;

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
        if (isOpen)
            SpeakerApi.Play("ElevatorOpen1.ogg", "KeycardDoorOpeningSound", _schematicObject?.Position ?? Position, true);
        _schematicObject?.AnimationController.Play(isOpen ? "door1" : "door3");
    }

    // ===== Options (Save/Load) =====

    public override Dictionary<string, string> CollectOptions()
    {
        return new Dictionary<string, string>
        {
            ["KeycardPermissions"]    = KeycardPermissions.ToString(),
            ["RequireAllPermissions"] = RequireAllPermissions.ToString(),
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

        // IsOpenはOnCreate後のCallDelayed内でアニメーションが再生されるため、
        // フィールドを直接セットしてアニメーションはOnCreate側に任せる
        if (options.TryGetValue("IsOpen", out var io)
            && bool.TryParse(io, out var isOpen))
        {
            IsOpen = isOpen;
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
                    // args[2以降] を "|" でjoinしてカンマ区切りに変換してパース
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