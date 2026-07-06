using System;
using System.Collections.Generic;
using System.Reflection;
using Exiled.API.Features;
using LabApi.Events.Arguments.PlayerEvents;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps.Features;
using UnityEngine;
using Player = Exiled.API.Features.Player;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

public class Document : ObjectPrefab
{
    private const string SchematicNameOption = "SchematicName";
    private const string DefaultSchematicName = "Document";
    private const float DefaultInteractionDuration = 3f;

    private string _modelSchematicName = DefaultSchematicName;
    private bool _showModel = true;
    private float _interactionDuration = DefaultInteractionDuration;

    /// <summary>
    /// このDocumentの種類。DocumentDictionaryから内容を引くときに使用。
    /// </summary>
    public DocumentType DocumentType { get; set; } = DocumentType.Scp033;

    /// <summary>
    /// 表示に使うスキマティック名。保存/マーカー Option では SchematicName として扱う。
    /// </summary>
    public string ModelSchematicName
    {
        get => _modelSchematicName;
        set => SetModelSchematicName(value);
    }

    /// <summary>
    /// モデル(Schematic)を表示するかどうか。falseの場合、インタラクタブルのみスポーンする。
    /// </summary>
    public bool ShowModel
    {
        get => _showModel;
        set
        {
            if (_showModel == value)
                return;

            _showModel = value;
            RefreshModel();
        }
    }

    /// <summary>
    /// Documentを読み取るために必要な長押し時間。
    /// </summary>
    public float InteractionDuration
    {
        get => _interactionDuration;
        set
        {
            float normalized = NormalizeInteractionDuration(value);
            if (_interactionDuration == normalized)
                return;

            _interactionDuration = normalized;
            ApplyInteractionDuration();
        }
    }

    private bool ShouldSpawnModel => _showModel && !string.IsNullOrWhiteSpace(_modelSchematicName);

    protected override void OnCreate()
    {
        RefreshModel();
    }

    protected override void OnSetup()
    {
        AddInteractable(duration: _interactionDuration, scale: Vector3.one * 0.75f);
    }

    protected override void OnToySearchedNearby(PlayerSearchedToyEventArgs? ev)
    {
        if (ev?.Player == null)
            return;

        var player = Player.Get(ev.Player);
        if (player == null || !player.IsConnected)
            return;

        var pos = Schematic?.Position ?? Position;
        SpeakerApi.Play("PickItem0.ogg", "Vent", pos, true, null, false, 2.5f, 0f);

        player.ShowHint(DocumentDictionary.Get(DocumentType), ServerSpecificUserSettings.GetDocumentHintDuration(player));
    }

    public override Dictionary<string, string> CollectOptions()
    {
        Dictionary<string, string> options = base.CollectOptions();
        options[SchematicNameOption] = _modelSchematicName;
        return options;
    }

    public override void ApplyOptions(Dictionary<string, string> options)
    {
        if (options == null || options.Count == 0)
            return;

        foreach (KeyValuePair<string, string> option in options)
        {
            if (IsSchematicNameOption(option.Key))
                SetModelSchematicName(option.Value);
        }

        base.ApplyOptions(options);
    }

    protected override bool IsAutomaticOptionProperty(PropertyInfo property)
        => !string.Equals(property.Name, nameof(ModelSchematicName), StringComparison.Ordinal) &&
           base.IsAutomaticOptionProperty(property);

    private void SetModelSchematicName(string? schematicName)
    {
        string normalized = schematicName?.Trim() ?? string.Empty;
        if (string.Equals(_modelSchematicName, normalized, StringComparison.Ordinal))
            return;

        _modelSchematicName = normalized;
        RefreshModel();
    }

    private void RefreshModel()
    {
        if (string.IsNullOrEmpty(ObjectInstanceID))
            return;

        if (!ShouldSpawnModel)
        {
            if (Schematic == null)
                return;

            DestroyManagedSchematic();
            SyncManagedObjects();
            return;
        }

        try
        {
            SpawnManagedSchematic(_modelSchematicName);
        }
        catch (Exception e)
        {
            Log.Warn($"[Document] Failed to spawn schematic '{_modelSchematicName}': {e.Message}");
            SyncManagedObjects();
        }
    }

    private void ApplyInteractionDuration()
    {
        foreach (InteractableHandle handle in Interactables)
        {
            if (!handle.Toy.IsDestroyed)
                handle.Toy.InteractionDuration = _interactionDuration;
        }
    }

    private static bool IsSchematicNameOption(string key)
    {
        switch (NormalizeOptionKey(key))
        {
            case "schematic":
            case "schematicname":
            case "model":
            case "modelschematic":
            case "modelschematicname":
                return true;
            default:
                return false;
        }
    }

    private static string NormalizeOptionKey(string key)
        => (key ?? string.Empty).Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();

    private static float NormalizeInteractionDuration(float value)
        => float.IsNaN(value) || float.IsInfinity(value) ? DefaultInteractionDuration : Math.Max(0f, value);

    // 主な Option:
    //   SchematicName=<schematic> / ShowModel=<bool> / InteractionDuration=<seconds> / DocumentType=<type>
}
