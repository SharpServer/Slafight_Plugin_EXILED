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
    private static readonly Vector3 InteractableLocalOffset = Vector3.zero;
    private static readonly Vector3 InteractableBaseScale = Vector3.one * 1.5f + Vector3.up * 2f;

    private static readonly Action<string, string, Vector3, bool, Transform, bool, float, float> CreateAndPlayAudio
        = EventHandler.CreateAndPlayAudio;

    protected override void OnCreate()
    {
         _schematicObject = SpawnManagedSchematic("trashbox");

         Timing.CallDelayed(0.5f, CreateInteractableToy);
         base.OnCreate();
    }

    private void CreateInteractableToy()
    {
        _interactableToy = CreateManagedInteractable(
            interactionDuration: 5f,
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

    protected override void OnToySearchedNearby(PlayerSearchedToyEventArgs ev)
    {
        var player = Player.Get(ev.Player);
        var pos = _schematicObject?.Position ?? Position;
        player?.ShowHint("ゴミ箱を漁った・・・中には、何も入っていなかった。");
    }
}
