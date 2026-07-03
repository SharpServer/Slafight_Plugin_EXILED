using System;
using System.Collections.Generic;
using Exiled.API.Features;
using Hints;
using UnityEngine;
using UserSettings.ServerSpecific;

namespace Slafight_Plugin_EXILED.API.Features;

public static class ServerSpecificUserSettings
{
    private const string KeybindUsageHintFormat = "<size=22>{0}で{1}。</size>\n<size=20>{2}</size>";
    private const string AssignedKeyTextFormat = "<color=#aaffaa>{0}</color>（{1}）";
    private const string KeybindSettingsTextFormat = "未設定・変更は 設定 > Server Specifics > {0} からできます。";
    private const string KeybindSettingsTextWithSuggestedFormat = "未設定・変更は 設定 > Server Specifics > {0} からできます（推奨: {1}）。";
    private const string KeybindParameterPlaceholder = "{0}";

    public sealed class KeybindHintContent(string text, HintParameter[] parameters)
    {
        public string Text { get; } = text;
        public HintParameter[] Parameters { get; } = parameters;
    }

    public static bool TryGetSetting<T>(Player? player, int settingId, out T setting)
        where T : ServerSpecificSettingBase
    {
        setting = null!;

        if (!TryGetSettings(player, out var settings))
            return false;

        foreach (var entry in settings)
        {
            if (entry != null && entry.SettingId == settingId && entry is T typed)
            {
                setting = typed;
                return true;
            }
        }

        return false;
    }

    public static bool TryGetKeybindPressed(Player? player, int settingId, out bool isPressed)
    {
        if (TryGetSetting<SSKeybindSetting>(player, settingId, out var setting))
        {
            isPressed = setting.SyncIsPressed;
            return true;
        }

        isPressed = false;
        return false;
    }

    public static bool IsKeybindPressed(Player? player, int settingId)
        => TryGetKeybindPressed(player, settingId, out var isPressed) && isPressed;

    public static bool TrySetKeybindPressed(Player? player, int settingId, bool isPressed)
    {
        if (!TryGetHub(player, out var hub))
            return false;

        var setting = ServerSpecificSettingsSync.GetSettingOfUser<SSKeybindSetting>(hub, settingId);
        if (setting == null)
            return false;

        setting.SyncIsPressed = isPressed;
        return true;
    }

    public static KeybindHintContent BuildKeybindUsageHint(Player? player, int settingId, string actionText)
    {
        string label = GetSettingLabel<SSKeybindSetting>(settingId, "Server Specificsキー");
        string suggested = GetSuggestedKeyText(settingId);

        string keyText = string.Format(AssignedKeyTextFormat, KeybindParameterPlaceholder, label);
        string settingsText = string.IsNullOrEmpty(suggested)
            ? string.Format(KeybindSettingsTextFormat, label)
            : string.Format(KeybindSettingsTextWithSuggestedFormat, label, suggested);

        return new KeybindHintContent(
            string.Format(KeybindUsageHintFormat, keyText, actionText, settingsText),
            [(HintParameter)new SSKeybindHintParameter(settingId)]);
    }

    public static string GetSuggestedKeyText(int settingId)
    {
        if (!TryGetDefinition<SSKeybindSetting>(settingId, out var setting))
            return string.Empty;

        return FormatKeyCode(setting.SuggestedKey);
    }

    public static bool TryGetText(Player? player, int settingId, out string text)
    {
        if (TryGetSetting<SSPlaintextSetting>(player, settingId, out var setting))
        {
            text = setting.SyncInputText ?? string.Empty;
            return true;
        }

        text = string.Empty;
        return false;
    }

    public static string GetText(Player? player, int settingId, string fallback = "")
        => TryGetText(player, settingId, out var text) ? text : fallback;

    public static bool TrySetText(Player? player, int settingId, string? text)
    {
        if (!TryGetHub(player, out var hub))
            return false;

        var setting = ServerSpecificSettingsSync.GetSettingOfUser<SSPlaintextSetting>(hub, settingId);
        if (setting == null)
            return false;

        setting.SyncInputText = text ?? string.Empty;
        return true;
    }

    public static bool TryGetSliderValue(Player? player, int settingId, out float value)
    {
        if (TryGetSetting<SSSliderSetting>(player, settingId, out var setting))
        {
            value = setting.SyncFloatValue;
            return true;
        }

        value = 0f;
        return false;
    }

    public static float GetSliderValue(Player? player, int settingId, float fallback)
    {
        if (TryGetSliderValue(player, settingId, out var value))
            return value;

        return TryGetDefinition<SSSliderSetting>(settingId, out var definition)
            ? definition.DefaultValue
            : fallback;
    }

    public static bool TrySetSliderValue(Player? player, int settingId, float value)
    {
        if (!TryGetHub(player, out var hub))
            return false;

        var setting = ServerSpecificSettingsSync.GetSettingOfUser<SSSliderSetting>(hub, settingId);
        if (setting == null)
            return false;

        setting.SyncFloatValue = value;
        return true;
    }

    public static bool TryGetTwoButtonIsB(Player? player, int settingId, out bool isB)
    {
        if (TryGetSetting<SSTwoButtonsSetting>(player, settingId, out var setting))
        {
            isB = setting.SyncIsB;
            return true;
        }

        isB = false;
        return false;
    }

    public static bool GetTwoButtonIsB(Player? player, int settingId, bool fallbackIsB = false)
    {
        if (TryGetTwoButtonIsB(player, settingId, out var isB))
            return isB;

        return TryGetDefinition<SSTwoButtonsSetting>(settingId, out var definition)
            ? definition.DefaultIsB
            : fallbackIsB;
    }

    public static bool GetTwoButtonIsA(Player? player, int settingId, bool fallbackIsB = false)
        => !GetTwoButtonIsB(player, settingId, fallbackIsB);

    public static bool TrySetTwoButtonIsB(Player? player, int settingId, bool isB)
    {
        if (!TryGetHub(player, out var hub))
            return false;

        var setting = ServerSpecificSettingsSync.GetSettingOfUser<SSTwoButtonsSetting>(hub, settingId);
        if (setting == null)
            return false;

        setting.SyncIsB = isB;
        return true;
    }

    public static string GetRpNameInput(Player? player)
        => GetText(player, ServerSpecifics.RpNameSettingId);

    public static bool TryGetPasscode(Player? player, out string passcode)
        => TryGetText(player, ServerSpecifics.SecretPasscodeSettingId, out passcode);

    public static float GetDocumentHintDuration(Player? player)
        => ClampDocumentHintDuration(GetSliderValue(
            player,
            ServerSpecifics.DocumentHintDurationSettingId,
            ServerSpecifics.DefaultDocumentHintDuration));

    public static bool IsAccessibilityModeEnabled(Player? player)
        => GetTwoButtonIsB(player, ServerSpecifics.AccessibilityModeSettingId);

    public static bool IsDebugModeSelected(Player? player)
        => GetTwoButtonIsA(player, ServerSpecifics.DebugModeSettingId, fallbackIsB: true);

    public static void RemovePlayer(Player? player)
    {
        if (!TryGetHub(player, out var hub))
            return;

        ServerSpecificSettingsSync.ReceivedUserSettings.Remove(hub);
        ServerSpecificSettingsSync.ReceivedUserStatuses.Remove(hub);
    }

    public static void ClearSetting(Player? player, int settingId)
    {
        if (!TryGetSettings(player, out var settings))
            return;

        settings.RemoveAll(setting => setting != null && setting.SettingId == settingId);
    }

    public static void ClearSettingFromAll(int settingId)
    {
        foreach (var settings in ServerSpecificSettingsSync.ReceivedUserSettings.Values)
        {
            if (settings == null)
                continue;

            settings.RemoveAll(setting => setting != null && setting.SettingId == settingId);
        }
    }

    public static void ClearAll()
    {
        foreach (var settings in ServerSpecificSettingsSync.ReceivedUserSettings.Values)
        {
            if (settings == null)
                continue;

            settings.RemoveAll(setting => setting != null && ServerSpecifics.IsManagedSettingId(setting.SettingId));
        }
    }

    private static float ClampDocumentHintDuration(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return ServerSpecifics.DefaultDocumentHintDuration;

        return Math.Max(
            ServerSpecifics.MinDocumentHintDuration,
            Math.Min(ServerSpecifics.MaxDocumentHintDuration, value));
    }

    private static bool TryGetSettings(Player? player, out List<ServerSpecificSettingBase> settings)
    {
        settings = null!;

        return TryGetHub(player, out var hub) &&
               ServerSpecificSettingsSync.ReceivedUserSettings.TryGetValue(hub, out settings) &&
               settings != null;
    }

    private static bool TryGetHub(Player? player, out ReferenceHub hub)
    {
        hub = null!;

        try
        {
            if (player?.ReferenceHub == null)
                return false;

            hub = player.ReferenceHub;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetDefinition<T>(int settingId, out T setting)
        where T : ServerSpecificSettingBase
    {
        setting = null!;
        var definitions = ServerSpecificSettingsSync.DefinedSettings ?? ServerSpecifics.Settings();

        foreach (var definition in definitions)
        {
            if (definition != null && definition.SettingId == settingId && definition is T typed)
            {
                setting = typed;
                return true;
            }
        }

        return false;
    }

    private static string GetSettingLabel<T>(int settingId, string fallback)
        where T : ServerSpecificSettingBase
        => TryGetDefinition<T>(settingId, out var setting) && !string.IsNullOrEmpty(setting.Label)
            ? setting.Label
            : fallback;

    private static string FormatKeyCode(KeyCode keyCode)
        => keyCode switch
        {
            KeyCode.None => string.Empty,
            KeyCode.Mouse0 => "左クリック",
            KeyCode.Mouse1 => "右クリック",
            KeyCode.Mouse2 => "中マウスボタン",
            KeyCode.LeftAlt => "左Alt",
            KeyCode.RightAlt => "右Alt",
            KeyCode.LeftControl => "左Ctrl",
            KeyCode.RightControl => "右Ctrl",
            KeyCode.LeftShift => "左Shift",
            KeyCode.RightShift => "右Shift",
            KeyCode.LeftArrow => "左矢印",
            KeyCode.RightArrow => "右矢印",
            KeyCode.Space => "Space",
            _ => keyCode.ToString(),
        };
}
