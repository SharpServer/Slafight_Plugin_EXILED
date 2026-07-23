using System;
using System.Collections.Generic;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems.IntermediateBases;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

/// <summary>
/// AccessTuner / DataCell のモデルを最初から内蔵した箱。
/// スキマティック側の ObjectPrefabSchematicInfo キー規約:
///   Interactable ブロック: "AccessTuner" / "LeftSide" / "RightSide"
///   対応するモデルブロック: "AccessTunerModel" / "LeftSideModel" / "RightSideModel"
///   レベル別バリアント（Model の子に全レベル分を内蔵）:
///     "AccessTunerModelLv0"（Broken）〜"AccessTunerModelLv3"
///     "LeftSideModelLv1"〜"LeftSideModelLv3" / "RightSideModelLv1"〜"RightSideModelLv3"
/// Interactable の長押しで取得判定を行い、取得されたパーツはモデルごと非表示化される。
/// オプションで無効化されたパーツも破棄せず、Visibility のみで非表示化される。
/// レベルバリアントは選択レベルのみ Spawn され、レベルオプションのランタイム変更にも追従する。
/// </summary>
public class AccessTunerBox : ObjectPrefab
{
    private const string TunerKey = "AccessTuner";
    private const string LeftKey = "LeftSide";
    private const string RightKey = "RightSide";
    private const string ModelKeySuffix = "Model";
    private const int BrokenTunerLevel = 0;
    private const int MinDataCellLevel = 1;
    private const int MaxLevel = 3;

    private readonly HashSet<string> _consumedParts = new(StringComparer.OrdinalIgnoreCase);
    private bool _isSetup;

    protected override string SchematicName => "AccessTunerCase";

    public bool SpawnAccessTuner
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            ApplyAllPartVisibility();
        }
    } = true;

    /// <summary>AccessTuner パーツのレベル。0 = Broken、1〜3 = Lv1〜Lv3。</summary>
    public Option SpawnAccessTunerLevel { get; } = new(
        OptionPart.Int("AccessTuner_level", BrokenTunerLevel, min: BrokenTunerLevel, max: MaxLevel));

    public Option SpawnDataCell { get; } = new(
        OptionPart.Bool("LeftSide", true),
        OptionPart.Bool("RightSide", true));

    public Option SpawnDataCellLevel { get; } = new(
        OptionPart.Int("LeftSide_level", 1, min: MinDataCellLevel, max: MaxLevel),
        OptionPart.Int("RightSide_level", 1, min: MinDataCellLevel, max: MaxLevel));

    protected override void OnSetup()
    {
        SetupTunerPart();
        SetupDataCellPart(LeftKey, "LeftSide_level");
        SetupDataCellPart(RightKey, "RightSide_level");

        _isSetup = true;
        ApplyAllPartVisibility();

        SpawnAccessTunerLevel.Changed += ApplyAllPartVisibility;
        SpawnDataCell.Changed += ApplyAllPartVisibility;
        SpawnDataCellLevel.Changed += ApplyAllPartVisibility;
    }

    private void ApplyAllPartVisibility()
    {
        if (!_isSetup)
            return;

        ApplyPartVisibility(
            TunerKey,
            SpawnAccessTuner,
            SpawnAccessTunerLevel.Get<int>("AccessTuner_level"),
            BrokenTunerLevel);
        ApplyPartVisibility(
            LeftKey,
            SpawnDataCell.Get<bool>(LeftKey),
            SpawnDataCellLevel.Get<int>("LeftSide_level"),
            MinDataCellLevel);
        ApplyPartVisibility(
            RightKey,
            SpawnDataCell.Get<bool>(RightKey),
            SpawnDataCellLevel.Get<int>("RightSide_level"),
            MinDataCellLevel);
    }

    private void ApplyPartVisibility(string key, bool configuredVisible, int level, int minLevel)
    {
        bool visible = configuredVisible && !_consumedParts.Contains(key);

        if (GetInteractable(key) is { } handle)
        {
            handle.Enabled = visible;
            if (!handle.Toy.IsDestroyed)
                handle.Toy.IsLocked = !visible;
        }

        SetBlockSpawned(key, visible);
        SetBlockSpawned(key + ModelKeySuffix, visible);
        ApplyLevelModelVisibility(key, level, minLevel, visible);
    }

    /// <summary>選択レベルのバリアントブロックだけを Spawn 状態にする。</summary>
    private void ApplyLevelModelVisibility(string key, int level, int minLevel, bool partVisible)
    {
        for (int lv = minLevel; lv <= MaxLevel; lv++)
            SetBlockSpawned($"{key}{ModelKeySuffix}Lv{lv}", partVisible && lv == level);
    }

    private void SetupTunerPart()
    {
        InteractableHandle? handle = PreparePart(TunerKey);
        if (handle == null)
            return;

        handle.Interacted += (player, _) =>
        {
            if (!handle.Enabled)
                return;

            int level = SpawnAccessTunerLevel.Get<int>("AccessTuner_level");
            AccessTunerBase? tuner = ResolveAccessTuner(level);
            if (tuner == null)
            {
                Log.Warn($"[AccessTunerBox] AccessTuner_level '{level}' に対応する AccessTuner を解決できませんでした。");
                return;
            }

            player.GiveOrDrop(tuner);
            ConsumePart(TunerKey);
        };
    }

    private void SetupDataCellPart(string key, string levelKey)
    {
        InteractableHandle? handle = PreparePart(key);
        if (handle == null)
            return;

        handle.Interacted += (player, _) =>
        {
            if (!handle.Enabled)
                return;

            DataCellBase? dataCell = ResolveDataCell(SpawnDataCellLevel.Get<int>(levelKey));
            if (dataCell == null)
            {
                Log.Warn($"[AccessTunerBox] '{levelKey}' に対応する DataCell を解決できませんでした。");
                return;
            }

            // Tuner 未所持や Lv 不一致で消費されなかった場合はパーツを残す
            if (!dataCell.TryUse(player))
                return;

            ConsumePart(key);
        };
    }

    /// <summary>
    /// パーツの Interactable を取得する。
    /// </summary>
    private InteractableHandle? PreparePart(string key)
    {
        InteractableHandle? handle = GetInteractable(key);
        if (handle == null)
            Log.Warn($"[AccessTunerBox] スキマティックに Interactable '{key}' が見つかりません。");

        return handle;
    }

    /// <summary>取得済みパーツを破棄せず非表示化する。</summary>
    private void ConsumePart(string key)
    {
        _consumedParts.Add(key);
        ApplyAllPartVisibility();
    }

    private static AccessTunerBase? ResolveAccessTuner(int level) => level switch
    {
        1 => CItem.Get<AccessTunerLv1>(),
        2 => CItem.Get<AccessTunerLv2>(),
        3 => CItem.Get<AccessTunerLv3>(),
        _ => CItem.Get<AccessTunerBroken>(),
    };

    private static DataCellBase? ResolveDataCell(int level) => level switch
    {
        2 => CItem.Get<DataCellLv2>(),
        3 => CItem.Get<DataCellLv3>(),
        _ => CItem.Get<DataCellLv1>(),
    };
}
