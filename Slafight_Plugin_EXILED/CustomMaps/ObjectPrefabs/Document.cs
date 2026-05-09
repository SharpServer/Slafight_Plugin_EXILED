using System;
using System.Collections.Generic;
using AdminToys;
using Exiled.API.Features;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using MEC;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps.Features;
using UnityEngine;
using Player = Exiled.API.Features.Player;
using EventHandler = Slafight_Plugin_EXILED.MainHandlers.EventHandler;

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

    private static readonly Action<string, string, Vector3, bool, Transform, bool, float, float> CreateAndPlayAudio
        = EventHandler.CreateAndPlayAudio;

    protected override void OnCreate()
    {
        if (ShowModel)
            _schematicObject = ObjectSpawner.SpawnSchematic("Document", base.Position, base.Rotation);

        Timing.CallDelayed(0.5f, CreateInteractableToy);
        base.OnCreate();
    }

    private void CreateInteractableToy()
    {
        _interactableToy = InteractableToy.Create();
        _interactableToy.Position = _schematicObject?.Position ?? base.Position;
        _interactableToy.Rotation = _schematicObject?.Rotation ?? base.Rotation;
        _interactableToy.InteractionDuration = 3f;
        _interactableToy.Shape = InvisibleInteractableToy.ColliderShape.Box;
        _interactableToy.Scale = Vector3.one;
        _interactableToy.Spawn();

        Log.Info($"Document Interactable spawned at {_interactableToy.Position}");
        SyncWithSchematic();
    }

    protected override void OnDestroy()
    {
        _schematicObject?.Destroy();
        _interactableToy?.Destroy();
        _schematicObject = null;
        _interactableToy = null;
        Log.Debug("[Document] Destroyed");
        base.OnDestroy();
    }

    protected override void OnToySearchedNearby(PlayerSearchedToyEventArgs ev)
    {
        var player = Player.Get(ev.Player);
        var pos = _schematicObject?.Position ?? Position;
        CreateAndPlayAudio("PickItem0.ogg", "Vent", pos, true, null, false, 2.5f, 0f);
        
        player.ShowHint(DocumentDictionary.Get(DocumentType), 10f);
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
            ShowModel = show;
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
            ShowModel = val;
            // ランタイムでモデルの表示/非表示を切り替え
            if (val && _schematicObject == null)
            {
                _schematicObject = ObjectSpawner.SpawnSchematic("Document", Position, Rotation);
                SyncWithSchematic();
            }
            else if (!val && _schematicObject != null)
            {
                _schematicObject.Destroy();
                _schematicObject = null;
            }
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