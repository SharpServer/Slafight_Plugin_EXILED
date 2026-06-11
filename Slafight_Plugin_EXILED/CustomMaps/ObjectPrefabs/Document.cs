using System;
using System.Collections.Generic;
using AdminToys;
using Exiled.API.Features;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using MEC;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps.Features;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.MainHandlers;
using UnityEngine;
using Player = Exiled.API.Features.Player;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

public class Document : ObjectPrefab
{
    public override float ToySearchRadius { get; set; } = 1.75f;

    /// <summary>
    /// このDocumentの種類。DocumentDictionaryから内容を引くときに使用。
    /// </summary>
    public DocumentType DocumentType { get; set; } = DocumentType.Scp033;

    /// <summary>
    /// モデル(Schematic)を表示するかどうか。falseの場合、インタラクタブルのみスポーンする。
    /// </summary>
    public bool ShowModel { get; set; } = true;

    private SchematicObject? _schematicObject;
    private InteractableToy? _interactableToy;
    private static readonly Vector3 InteractableLocalOffset = Vector3.zero;
    private static readonly Vector3 InteractableBaseScale = Vector3.one;
    protected override void OnCreate()
    {
        if (ShowModel)
            _schematicObject = SpawnManagedSchematic("Document");

        Timing.CallDelayed(0.5f, CreateInteractableToy);
        base.OnCreate();
    }

    private void CreateInteractableToy()
    {
        _interactableToy = CreateManagedInteractable(
            interactionDuration: 3f,
            shape: InvisibleInteractableToy.ColliderShape.Box,
            localOffset: InteractableLocalOffset,
            baseScale: InteractableBaseScale);

        Log.Info($"Document Interactable spawned at {_interactableToy.Position}");
    }

    protected override void OnDestroy()
    {
        _schematicObject = null;
        _interactableToy = null;
        Log.Debug("[Document] Destroyed");
        base.OnDestroy();
    }

    protected override void OnToySearchedNearby(PlayerSearchedToyEventArgs? ev)
    {
        if (ev?.Player == null)
            return;

        var player = Player.Get(ev.Player);
        if (player == null || !player.IsConnected)
            return;

        var pos = _schematicObject?.Position ?? Position;
        SpeakerApi.Play("PickItem0.ogg", "Vent", pos, true, null, false, 2.5f, 0f);

        player.ShowHint(DocumentDictionary.Get(DocumentType), ServerSpecificsHandler.GetDocumentHintDuration(player));
    }

    // ===== Options (Save/Load) =====

    public override Dictionary<string, string> CollectOptions()
    {
        return new Dictionary<string, string>
        {
            ["DocumentType"] = DocumentType.ToString(),
            ["ShowModel"] = ShowModel.ToString()
        };
    }

    public override void ApplyOptions(Dictionary<string, string> options)
    {
        if (options.TryGetValue("DocumentType", out var val)
            && Enum.TryParse<DocumentType>(val, true, out var dt))
        {
            DocumentType = dt;
        }

        if (options.TryGetValue("ShowModel", out var sm)
            && bool.TryParse(sm, out var show))
        {
            SetShowModel(show);
        }
    }

    // ===== Mod Command =====

    public override bool HandleModCommand(ArraySegment<string> args, out string response)
    {
        if (args.Count >= 2 && args.At(1).Equals("showmodel", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Count < 3)
            {
                response = $"Current: {ShowModel}\nUsage: mod showmodel <true|false>";
                return true;
            }
            if (!bool.TryParse(args.At(2), out var val))
            {
                response = $"Invalid value '{args.At(2)}'. Use true or false.";
                return true;
            }
            SetShowModel(val);
            response = $"Set ShowModel to {val}.";
            return true;
        }

        if (args.Count >= 2 && args.At(1).Equals("documenttype", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Count < 3)
            {
                response = $"Current: {DocumentType}\nUsage: mod documenttype <{string.Join("|", Enum.GetNames(typeof(DocumentType)))}>";
                return true;
            }
            if (!Enum.TryParse<DocumentType>(args.At(2), true, out var dt))
            {
                response = $"Unknown DocumentType '{args.At(2)}'. Available: {string.Join(", ", Enum.GetNames(typeof(DocumentType)))}";
                return true;
            }
            DocumentType = dt;
            response = $"Set DocumentType to {dt}.";
            return true;
        }
        return base.HandleModCommand(args, out response);
    }

    private void SetShowModel(bool showModel)
    {
        ShowModel = showModel;
        if (string.IsNullOrEmpty(ObjectInstanceID))
            return;

        if (showModel && _schematicObject == null)
        {
            _schematicObject = SpawnManagedSchematic("Document");
        }
        else if (!showModel && _schematicObject != null)
        {
            DestroyManagedSchematic();
            _schematicObject = null;
            SyncManagedObjects();
        }
    }
}
