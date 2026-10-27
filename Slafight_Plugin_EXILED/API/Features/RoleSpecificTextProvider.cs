using System.Collections.Generic;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.API.Features;

public static class RoleSpecificTextProvider
{
    private static readonly Dictionary<int, string> _texts = new();

    /// <summary>
    /// ロール固有 HUD テキストを設定
    /// </summary>
    public static void Set(Player player, string text)
    {
        if (player == null) return;
        _texts[player.Id] = text;
    }

    /// <summary>
    /// ロール固有 HUD テキストを取得します。押し込まれた文字列が無ければ <see cref="CustomRole.Status"/> を読みます。
    /// </summary>
    public static string GetFor(Player player)
    {
        if (player == null) return string.Empty;

        if (_texts.TryGetValue(player.Id, out var text) && !string.IsNullOrEmpty(text))
            return text;

        return CustomRole.Of(player)?.Status ?? string.Empty;
    }

    /// <summary>
    /// テキストをクリア
    /// </summary>
    public static void Clear(Player player)
    {
        if (player == null) return;
        _texts.Remove(player.Id);
    }

    public static void ClearAll()
    {
        _texts.Clear();
    }
}
