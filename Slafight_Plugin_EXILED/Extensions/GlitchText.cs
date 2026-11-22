#nullable enable

using Slafight_Plugin_EXILED.API.Structs;
using System.Text;
using UnityEngine;
using Random = System.Random;

namespace Slafight_Plugin_EXILED.Extensions;

/// <summary>
/// 「壊れた画面」風テキストを 1 パスで組み立てるユーティリティ。毎 tick 呼んでも軽い。
/// </summary>
public static class GlitchText
{
    /// <summary>
    /// 文字化けに使うグリフ。TMP がタグとして解釈してしまう '&lt;' '&gt;' は含めない。
    /// このリポジトリの Document 類で描画実績のある '■' '█' と、ASCII 記号・全角カタカナのみを使う。
    /// </summary>
    private const string GlitchGlyphs =
        "■█#@$%&*/\\|_=+~^?!;:.,'\"()[]{}0123456789" +
        "アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲン";

    private static readonly string[] ColorTags = BuildColorTags(48, 1f, 1f);
    private static readonly string[] DimColorTags = BuildColorTags(24, 0.45f, 0.55f);

    /// <summary>
    /// ランダムな開始タグ (<c>&lt;color=#RRGGBB&gt;</c>) を返す。閉じタグは呼び出し側で付ける。
    /// </summary>
    public static string RandomColorTag(Random rng, bool dim = false)
    {
        string[] tags = GetColorTags(dim);
        return tags[rng.Next(tags.Length)];
    }

    /// <summary>
    /// ランダムな文字化けグリフを 1 文字返す。
    /// </summary>
    public static char RandomGlyph(Random rng) => GlitchGlyphs[rng.Next(GlitchGlyphs.Length)];

    /// <summary>
    /// <paramref name="text"/> を「一定文字数ごとに色が変わる」「一定確率で文字化けする」形で
    /// <paramref name="sb"/> へ追記する。
    /// </summary>
    public static void AppendGlitched(
        StringBuilder sb,
        string text,
        int colorRunLength,
        float corruptChance,
        Random rng,
        bool dim = false)
    {
        if (string.IsNullOrEmpty(text))
            return;

        GlitchWriter writer = new(sb, colorRunLength, corruptChance, rng, dim);
        writer.Feed(text);
        writer.End();
    }

    internal static string[] GetColorTags(bool dim) => dim ? DimColorTags : ColorTags;

    private static string[] BuildColorTags(int count, float saturation, float value)
    {
        var tags = new string[count];

        for (int i = 0; i < count; i++)
        {
            Color color = Color.HSVToRGB(i / (float)count, saturation, value);
            tags[i] = "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">";
        }

        return tags;
    }
}
