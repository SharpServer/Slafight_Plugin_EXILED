#nullable enable
using System;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Hints;

/// <summary>デバッグ表示に並べる項目です。</summary>
[Flags]
public enum DebugHudSection
{
    None = 0,
    Role = 1 << 0,
    Position = 1 << 1,
    Round = 1 << 2,
    Warhead = 1 << 3,
    Item = 1 << 4,
    Inventory = 1 << 5,
    Armor = 1 << 6,
    Effects = 1 << 7,
}

/// <summary>
/// デバッグ表示の個人設定です。Server Specifics の値から毎回組み立てます。
/// </summary>
public readonly struct DebugHudOptions
{
    private static readonly (DebugHudSection Section, int SettingId)[] SectionSettings =
    [
        (DebugHudSection.Role, ServerSpecifics.DebugHudShowRoleSettingId),
        (DebugHudSection.Position, ServerSpecifics.DebugHudShowPositionSettingId),
        (DebugHudSection.Round, ServerSpecifics.DebugHudShowRoundSettingId),
        (DebugHudSection.Warhead, ServerSpecifics.DebugHudShowWarheadSettingId),
        (DebugHudSection.Item, ServerSpecifics.DebugHudShowItemSettingId),
        (DebugHudSection.Inventory, ServerSpecifics.DebugHudShowInventorySettingId),
        (DebugHudSection.Armor, ServerSpecifics.DebugHudShowArmorSettingId),
        (DebugHudSection.Effects, ServerSpecifics.DebugHudShowEffectsSettingId),
    ];

    private DebugHudOptions(int x, int y, int textSize, bool compact, DebugHudSection sections)
    {
        X = x;
        Y = y;
        TextSize = textSize;
        Compact = compact;
        Sections = sections;
    }

    /// <summary>ヒントの横位置です。</summary>
    public int X { get; }

    /// <summary>ヒントの縦位置です。</summary>
    public int Y { get; }

    /// <summary>本文の文字サイズです。</summary>
    public int TextSize { get; }

    /// <summary>詳細行を省くか。</summary>
    public bool Compact { get; }

    /// <summary>表示する項目です。</summary>
    public DebugHudSection Sections { get; }

    public bool Shows(DebugHudSection section) => (Sections & section) != 0;

    public static DebugHudOptions Of(Player? player)
    {
        var sections = DebugHudSection.None;
        foreach (var (section, settingId) in SectionSettings)
        {
            if (ServerSpecificUserSettings.GetTwoButtonIsA(player, settingId))
                sections |= section;
        }

        return new DebugHudOptions(
            ReadInt(player, ServerSpecifics.DebugHudXSettingId, ServerSpecifics.DefaultDebugHudX, ServerSpecifics.MinDebugHudX, ServerSpecifics.MaxDebugHudX),
            ReadInt(player, ServerSpecifics.DebugHudYSettingId, ServerSpecifics.DefaultDebugHudY, ServerSpecifics.MinDebugHudY, ServerSpecifics.MaxDebugHudY),
            ReadInt(player, ServerSpecifics.DebugHudTextSizeSettingId, ServerSpecifics.DefaultDebugHudTextSize, ServerSpecifics.MinDebugHudTextSize, ServerSpecifics.MaxDebugHudTextSize),
            ServerSpecificUserSettings.GetTwoButtonIsB(player, ServerSpecifics.DebugHudCompactSettingId),
            sections);
    }

    private static int ReadInt(Player? player, int settingId, int fallback, int min, int max)
    {
        float value = ServerSpecificUserSettings.GetSliderValue(player, settingId, fallback);
        if (float.IsNaN(value) || float.IsInfinity(value))
            return fallback;

        return Mathf.Clamp(Mathf.RoundToInt(value), min, max);
    }
}
