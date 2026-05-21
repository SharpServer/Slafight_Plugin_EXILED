using System;
using AdminToys;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using MEC;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;
using Player = Exiled.API.Features.Player;
using EventHandler = Slafight_Plugin_EXILED.MainHandlers.EventHandler;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

public class Trashbox : ObjectPrefab
{
    public override float ToySearchRadius { get; set; } = 1.2f;

    private SchematicObject? _schematicObject;
    private InteractableToy? _interactableToy;

    private static readonly Action<string, string, Vector3, bool, Transform, bool, float, float> CreateAndPlayAudio
        = EventHandler.CreateAndPlayAudio;

    protected override void OnCreate()
    {
         _schematicObject = ObjectSpawner.SpawnSchematic("trashbox", base.Position, base.Rotation);

        Timing.CallDelayed(0.5f, CreateInteractableToy);
        base.OnCreate();
    }

    private void CreateInteractableToy()
    {
        _interactableToy = InteractableToy.Create();
        _interactableToy.Position = _schematicObject?.Position ?? base.Position;
        _interactableToy.Position += Vector3.zero;
        _interactableToy.Rotation = _schematicObject?.Rotation ?? base.Rotation;
        _interactableToy.InteractionDuration = 5f;
        _interactableToy.Shape = InvisibleInteractableToy.ColliderShape.Box;
        _interactableToy.Scale = Vector3.one * 1.5f + Vector3.up * 2f;
        _interactableToy.Spawn();
        SyncWithSchematic();
    }

    protected override void OnDestroy()
    {
        _schematicObject?.Destroy();
        _interactableToy?.Destroy();
        _schematicObject = null;
        _interactableToy = null;
        base.OnDestroy();
    }

    protected override void OnToySearchedNearby(PlayerSearchedToyEventArgs ev)
    {
        var player = Player.Get(ev.Player);
        var pos = _schematicObject?.Position ?? Position;
        player?.ShowHint("ゴミ箱を漁った・・・中には、何も入っていなかった。");
    }

    // ===== Position/Rotation/Scale sync =====

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
                _interactableToy?.Scale = value * 1.2f;
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