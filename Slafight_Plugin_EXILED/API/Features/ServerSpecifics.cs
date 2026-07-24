using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UserSettings.ServerSpecific;

namespace Slafight_Plugin_EXILED.API.Features;

public abstract class ServerSpecifics
{
    private enum SettingKey
    {
        Header,
        Banner,
        CommunicationGroup,
        ProximityChatKeybind,
        RpName,
        AbilityGroup,
        AbilityUseKeybind,
        AbilitySwitchKeybind,
        AbilityOptionPreviousKeybind,
        AbilityOptionNextKeybind,
        ItemGroup,
        ItemModeSwitchKeybind,
        SuicideButtonKeybind,
        DisplayGroup,
        DocumentHintDuration,
        AccessibilityMode,
        SpecialGroup,
        SecretPasscode,
        DebugMode,
    }

    private sealed class SettingDefinition
    {
        public SettingDefinition(SettingKey key, Func<int, ServerSpecificSettingBase> factory)
        {
            Key = key;
            Factory = factory;
        }

        public SettingKey Key { get; }

        public Func<int, ServerSpecificSettingBase> Factory { get; }
    }

    // このリストの並び順が SS UI の並び順であり、そのまま SettingId になります。
    // 新しい設定を挟む時はここへ追加/移動するだけで、公開 SettingId も自動で追従します。
    private static readonly List<SettingDefinition> SettingDefinitions =
    [
        new SettingDefinition(
            SettingKey.Header,
            id => new SSGroupHeader(
                id,
                "シャープ鯖 Server Specifics",
                hint: "シャープ鯖の個人設定です。")),


        new SettingDefinition(
            SettingKey.Banner,
            id => new SSTextArea(
                id,
                "<b>よく使う設定を用途別に整理しました</b>\n<size=80%>VC/RP ・ アビリティ ・ アイテム ・ 表示 ・ 特殊設定</size>",
                SSTextArea.FoldoutMode.NotCollapsable,
                null,
                TextAlignmentOptions.Center)),


        new SettingDefinition(
            SettingKey.CommunicationGroup,
            id => new SSGroupHeader(
                id,
                "VC / RP",
                hint: "会話とRP表示に関する設定です。")),


        new SettingDefinition(
            SettingKey.ProximityChatKeybind,
            id => new SSKeybindSetting(
                id,
                "近接チャット",
                KeyCode.V,
                true,
                false,
                hint: "一部の利用可能ロールで、近接チャットを使用するのに必要です。Vを推奨します。")),


        new SettingDefinition(
            SettingKey.RpName,
            id => new SSPlaintextSetting(
                id,
                "RPキャラクター名",
                "",
                20,
                hint: "RPのキャラ名です。設定した名前の後に本当の名前が表示されます。")),


        new SettingDefinition(
            SettingKey.AbilityGroup,
            id => new SSGroupHeader(
                id,
                "アビリティ操作",
                hint: "HUD の Ability 表示に対応する操作キーです。")),


        new SettingDefinition(
            SettingKey.AbilityUseKeybind,
            id => new SSKeybindSetting(
                id,
                "アビリティ使用",
                KeyCode.LeftAlt,
                true,
                false,
                hint: "HUDの Ability に表示されている選択中アビリティを発動します。左Altを推奨します。")),


        new SettingDefinition(
            SettingKey.AbilitySwitchKeybind,
            id => new SSKeybindSetting(
                id,
                "アビリティ切り替え",
                KeyCode.Mouse2,
                true,
                false,
                hint: "HUDに Ability 1/2 のように表示されている場合、次のアビリティへ切り替えます。中マウスボタンを推奨します。")),


        new SettingDefinition(
            SettingKey.AbilityOptionPreviousKeybind,
            id => new SSKeybindSetting(
                id,
                "アビリティオプション左",
                KeyCode.LeftArrow,
                true,
                false,
                hint: "選択中アビリティに複数のオプションがある場合、前のオプションへ切り替えます。左矢印キーを推奨します。")),


        new SettingDefinition(
            SettingKey.AbilityOptionNextKeybind,
            id => new SSKeybindSetting(
                id,
                "アビリティオプション右",
                KeyCode.RightArrow,
                true,
                false,
                hint: "選択中アビリティに複数のオプションがある場合、次のオプションへ切り替えます。右矢印キーを推奨します。")),


        new SettingDefinition(
            SettingKey.ItemGroup,
            id => new SSGroupHeader(
                id,
                "アイテム / 緊急操作",
                hint: "特殊アイテム操作と緊急用の入力です。")),


        new SettingDefinition(
            SettingKey.ItemModeSwitchKeybind,
            id => new SSKeybindSetting(
                id,
                "アイテムモード切り替え",
                KeyCode.G,
                true,
                false,
                hint: "Hybridアイテムのモードを切り替えます。Gを推奨します。")),


        new SettingDefinition(
            SettingKey.SuicideButtonKeybind,
            id => new SSKeybindSetting(
                id,
                "自害ボタン",
                KeyCode.K,
                true,
                false,
                hint: "銃器や近接武器を所持しているときに自分に向けて攻撃します。Kを推奨します。")),


        new SettingDefinition(
            SettingKey.DisplayGroup,
            id => new SSGroupHeader(
                id,
                "表示 / アクセシビリティ",
                hint: "表示時間や見やすさに関する設定です。")),


        new SettingDefinition(
            SettingKey.DocumentHintDuration,
            id => new SSSliderSetting(
                id,
                "資料表示時間",
                MinDocumentHintDuration,
                MaxDocumentHintDuration,
                DefaultDocumentHintDuration,
                false,
                "0.#",
                "{0}秒",
                hint: "Documentを調べた時に内容を表示する秒数です。")),


        new SettingDefinition(
            SettingKey.AccessibilityMode,
            id => new SSTwoButtonsSetting(
                id,
                "アクセシビリティモード",
                "OFF",
                "ON",
                hint: "一部のモデルを見やすい表示へ切り替えます。")),


        new SettingDefinition(
            SettingKey.SpecialGroup,
            id => new SSGroupHeader(
                id,
                "特殊 / 管理者",
                hint: "特殊な入力と管理者向け設定です。")),


        new SettingDefinition(
            SettingKey.SecretPasscode,
            id => new SSPlaintextSetting(
                id,
                "シークレットパスコード",
                "00000",
                5,
                hint: "特別な場面で必要となるかもしれません・・・")),


        new SettingDefinition(
            SettingKey.DebugMode,
            id => new SSTwoButtonsSetting(
                id,
                "[ADMIN]デバッグモード",
                "ON",
                "OFF",
                true))
    ];

    private static readonly Dictionary<SettingKey, int> SettingIds = BuildSettingIds();

    public static readonly int HeaderSettingId = GetSettingId(SettingKey.Header);
    public static readonly int ProximityChatKeybindSettingId = GetSettingId(SettingKey.ProximityChatKeybind);
    public static readonly int RpNameSettingId = GetSettingId(SettingKey.RpName);
    public static readonly int AbilityUseKeybindSettingId = GetSettingId(SettingKey.AbilityUseKeybind);
    public static readonly int AbilitySwitchKeybindSettingId = GetSettingId(SettingKey.AbilitySwitchKeybind);
    public static readonly int AbilityOptionPreviousKeybindSettingId = GetSettingId(SettingKey.AbilityOptionPreviousKeybind);
    public static readonly int AbilityOptionNextKeybindSettingId = GetSettingId(SettingKey.AbilityOptionNextKeybind);
    public static readonly int ItemModeSwitchKeybindSettingId = GetSettingId(SettingKey.ItemModeSwitchKeybind);
    public static readonly int SuicideButtonKeybindSettingId = GetSettingId(SettingKey.SuicideButtonKeybind);
    public static readonly int SecretPasscodeSettingId = GetSettingId(SettingKey.SecretPasscode);
    public static readonly int DocumentHintDurationSettingId = GetSettingId(SettingKey.DocumentHintDuration);
    public static readonly int AccessibilityModeSettingId = GetSettingId(SettingKey.AccessibilityMode);
    public static readonly int DebugModeSettingId = GetSettingId(SettingKey.DebugMode);

    public const float DefaultDocumentHintDuration = 10f;
    public const float MinDocumentHintDuration = 1f;
    public const float MaxDocumentHintDuration = 60f;

    public static ServerSpecificSettingBase[] Settings()
    {
        var settings = new ServerSpecificSettingBase[SettingDefinitions.Count];

        for (int i = 0; i < SettingDefinitions.Count; i++)
            settings[i] = SettingDefinitions[i].Factory(i);

        return settings;
    }

    public static bool IsManagedSettingId(int settingId)
        => settingId >= 0 && settingId < SettingDefinitions.Count;

    private static Dictionary<SettingKey, int> BuildSettingIds()
    {
        var ids = new Dictionary<SettingKey, int>(SettingDefinitions.Count);

        for (int i = 0; i < SettingDefinitions.Count; i++)
        {
            var key = SettingDefinitions[i].Key;
            if (ids.ContainsKey(key))
                throw new InvalidOperationException($"Duplicate ServerSpecifics setting key: {key}");

            ids.Add(key, i);
        }

        return ids;
    }

    private static int GetSettingId(SettingKey key)
        => SettingIds[key];
}
