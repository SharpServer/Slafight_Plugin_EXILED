using System.Text;
using System;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.API.Structs;

/// <summary>
/// 1 行ぶんのグリッチ文字列を書き出す状態機械。<see cref="Feed(string)"/> の後に <see cref="End"/> を呼ぶ。
/// </summary>
/// <remarks>ミュータブルな struct なので、ローカル変数としてだけ使うこと。</remarks>
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
        _tags = GlitchText.GetColorTags(dim);
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
            ? GlitchText.RandomGlyph(_rng)
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
