using System;
using System.Collections.Generic;
using AdminToys;
using Exiled.API.Enums;
using Exiled.API.Features;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using MEC;
using PlayerRoles;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Player = Exiled.API.Features.Player;
using EventHandler = Slafight_Plugin_EXILED.MainHandlers.EventHandler;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

public class Vent : ObjectPrefab
{
    public override float ToySearchRadius { get; set; } = 1.75f;

    private SchematicObject? _schematicObject;
    private InteractableToy? _interactableToy;

    private static readonly Action<string, string, Vector3, bool, Transform, bool, float, float> CreateAndPlayAudio
        = EventHandler.CreateAndPlayAudio;

    private Dictionary<int, byte> _touchedDictionary = [];
    private Dictionary<int, double> _lastTouchTime = [];   // LocalTime 用
    private const double TouchTimeout = 30d;               // 30秒でリセット

    protected override void OnCreate()
    {
        _schematicObject = ObjectSpawner.SpawnSchematic("Vent", base.Position, base.Rotation);
        Timing.CallDelayed(0.5f, CreateInteractableToy);
        base.OnCreate();
    }

    private void CreateInteractableToy()
    {
        if (_schematicObject == null) return;

        _interactableToy = InteractableToy.Create();
        _interactableToy.Position = _schematicObject.Position;
        _interactableToy.Rotation = _schematicObject.Rotation;
        _interactableToy.InteractionDuration = 3f;
        _interactableToy.Shape = InvisibleInteractableToy.ColliderShape.Box;
        _interactableToy.Scale = Vector3.one;
        _interactableToy.Spawn();

        Log.Info($"Vent Interactable spawned at {_interactableToy.Position}");
        SyncWithSchematic();
    }

    protected override void OnDestroy()
    {
        _schematicObject?.Destroy();
        _interactableToy?.Destroy();
        _schematicObject = null;
        _interactableToy = null;
        Log.Debug("[Vent] Destroyed");
        base.OnDestroy();
    }

    protected override void OnToySearchingNearby(PlayerSearchingToyEventArgs ev)
    {
        var player = Player.Get(ev.Player);
        if (player is null)
            return;

        var now = Timing.LocalTime;

        // タイムアウトチェック
        if (_lastTouchTime.TryGetValue(player.Id, out var last) &&
            now - last > TouchTimeout)
        {
            _touchedDictionary.Remove(player.Id);
            _lastTouchTime.Remove(player.Id);
        }

        _lastTouchTime[player.Id] = now;

        var roleInfo = player.GetRoleInfo();
        if (player.GetTeam() == CTeam.ChaosInsurgency ||
            roleInfo is { Vanilla: RoleTypeId.Scp173, Custom: CRoleTypeId.Scp173 or CRoleTypeId.None })
            return;

        ev.IsAllowed = false;

        var touch = _touchedDictionary.GetValueOrDefault(player.Id, (byte)0);

        switch (touch)
        {
            case 0:
                player.ShowHint("<size=24>あなたのロールでは使用することが出来ません。</size>");
                touch = 1;
                break;
            case 1:
                player.ShowHint("<size=24>あなたのロールでは使用することが出来ません...</size>");
                touch = 2;
                break;
            case 2:
                player.ShowHint("<size=24>あなたのロールでは使用することが出来ません...!</size>");
                touch = 3;
                break;
            case 3:
                player.ShowHint("<size=24>あなたのロールでは使用することが出来ません...!!!!!</size>");
                touch = 4;
                break;
            case 4:
                player.ShowHint("<size=24>だから使用することが出来ないんだって!!!!!</size>");
                touch = 5;
                break;
            case 5:
                player.ShowHint("<size=24>諦めてくれって!!!!!</size>");
                touch = 6;
                break;
            case 6:
                player.ShowHint("<size=24>これ以上触ったらNullReferenceExceptionを起こしますよ！？</size>");
                touch = 7;
                break;
            default: // 7以上
                var innerEx = new UnauthorizedAccessException(
                    $"VentAccessViolation Details:\n" +
                    $"Player ID: {player.Id}\n" +
                    $"Team: {player.GetTeam()}\n" +
                    $"Role Vanilla: {roleInfo.Vanilla}\n" +
                    $"Role Custom: {roleInfo.Custom}\n\n" +
                    "Required Access:\n" +
                    "- CTeam.ChaosInsurgency\n" +
                    "OR\n" +
                    "- RoleTypeId.Scp173 + CRoleTypeId.Scp173/None"
                );

                var nre = new NullReferenceException("VentAccessViolation", innerEx);
                player.ShowHint($"<size=16><color=#FF4444>{nre}</color></size>", 7f);
                touch = 7; // これ以上は増やさない
                break;
        }

        _touchedDictionary[player.Id] = touch;
    }

    protected override void OnToySearchedNearby(PlayerSearchedToyEventArgs ev)
    {
        var player = Player.Get(ev.Player);
        if (_schematicObject == null)
            return;
    
        CreateAndPlayAudio("ventsound.ogg", "Vent", _schematicObject.Position, true, null, false, 10f, 0f);

        var currentRoomType = player.CurrentRoom?.Type ?? RoomType.Unknown;

        if (!GlobalVentManager.TryTriggerLoose(player, currentRoomType, out var point))
        {
            Log.Warn($"[Vent] No VentPoint (loose) from {currentRoomType} for {player.Nickname}");
            return;
        }

        Vector3 exitSoundPos;
        if (point.ExitWorldPosition != Vector3.zero)
            exitSoundPos = point.ExitWorldPosition;
        else if (point.ExitRoomType.HasValue)
            exitSoundPos = StaticUtils
                .GetWorldFromRoomLocal(point.ExitRoomType.Value, point.ExitLocalPosition, Vector3.zero)
                .worldPosition;
        else
            exitSoundPos = player.Position;

        Timing.CallDelayed(0.1f, () =>
            CreateAndPlayAudio("ventsound.ogg", "Vent", exitSoundPos, true, null, false, 10f, 0f));
    }

    protected override void OnRoundStarted()
    {
        _touchedDictionary = [];
        _lastTouchTime = [];
    }

    public override Vector3 Position
    {
        get => _schematicObject != null ? _schematicObject.Position : base.Position;
        set
        {
            if (_schematicObject != null)
            {
                _schematicObject.Position = value;
                SyncWithSchematic();
            }
            else
            {
                base.Position = value;
            }
        }
    }

    public override Quaternion Rotation
    {
        get => _schematicObject != null ? _schematicObject.Rotation : base.Rotation;
        set
        {
            if (_schematicObject != null)
            {
                _schematicObject.Rotation = value;
                SyncWithSchematic();
            }
            else
            {
                base.Rotation = value;
            }
        }
    }

    public override Vector3 Scale
    {
        get => _schematicObject != null ? _schematicObject.Scale : base.Scale;
        set
        {
            if (_schematicObject != null)
            {
                _schematicObject.Scale = value;
                if (_interactableToy != null)
                    _interactableToy.Scale = value * 1.2f;
            }
            else
            {
                base.Scale = value;
            }
        }
    }

    private void SyncWithSchematic()
    {
        if (_schematicObject == null || _interactableToy == null)
            return;

        _interactableToy.Position = _schematicObject.Position;
        _interactableToy.Rotation = _schematicObject.Rotation;
    }
}
