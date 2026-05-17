using System.Collections.Generic;
using Exiled.API.Features;

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
    /// ロール固有 HUD テキストを取得
    /// </summary>
    public static string GetFor(Player player)
    {
        if (player == null) return string.Empty;
        return _texts.TryGetValue(player.Id, out var text) ? text : string.Empty;
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
