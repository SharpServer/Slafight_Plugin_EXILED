#nullable enable

using System.Text;
using UnityEngine;
using Random = System.Random;

namespace Slafight_Plugin_EXILED.Extensions;

/// <summary>
/// 「壊れた画面」風テキストを 1 パスで組み立てるユーティリティ。
/// 色タグ文字列は静的にキャッシュ済みで、1 文字ごとの <see cref="ColorUtility"/> 呼び出しや
/// 中間 string を作らないため、毎 tick 呼び出しても負荷が乗りにくい。
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
        string[] tags = dim ? DimColorTags : ColorTags;
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

    /// <summary>
    /// 複数の元テキストを繋ぎながら 1 行ぶんのグリッチ文字列を書き出すための状態機械。
    /// <see cref="Feed(string)"/> を複数回呼んでから <see cref="End"/> を呼ぶこと。
    /// ミュータブルな struct なので、必ずローカル変数として使う（フィールドにコピーしない）。
    /// </summary>
    public struct GlitchWriter
    {
        private readonly StringBuilder _sb;
        private readonly string[] _tags;
        private readonly int _runLength;
        private readonly float _corruptChance;
        private readonly Random _rng;

        private int _remaining;
        private bool _colorOpen;

        public GlitchWriter(StringBuilder sb, int colorRunLength, float corruptChance, Random rng, bool dim = false)
        {
            _sb = sb;
            _tags = dim ? DimColorTags : ColorTags;
            _runLength = colorRunLength < 1 ? 1 : colorRunLength;
            _corruptChance = corruptChance;
            _rng = rng;
            _remaining = 0;
            _colorOpen = false;
            VisibleLength = 0;
        }

        /// <summary>ここまでに書き出した可視文字数（タグを除く）。</summary>
        public int VisibleLength { get; private set; }

        public void Feed(char c)
        {
            if (c == '\r')
                return;

            if (c == '\n')
            {
                CloseColor();
                _sb.Append('\n');
                _remaining = 0;
                return;
            }

            if (_remaining <= 0)
            {
                CloseColor();
                _sb.Append(_tags[_rng.Next(_tags.Length)]);
                _colorOpen = true;
                _remaining = _runLength;
            }

            _sb.Append(_corruptChance > 0f && _rng.NextDouble() < _corruptChance
                ? GlitchGlyphs[_rng.Next(GlitchGlyphs.Length)]
                : c);

            _remaining--;
            VisibleLength++;
        }

        public void Feed(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            foreach (char c in text)
                Feed(c);
        }

        public void End() => CloseColor();

        private void CloseColor()
        {
            if (!_colorOpen)
                return;

            _sb.Append("</color>");
            _colorOpen = false;
        }
    }
}
