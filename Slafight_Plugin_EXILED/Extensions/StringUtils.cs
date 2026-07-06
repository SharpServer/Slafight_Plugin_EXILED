using System;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Extensions;

public static class StringUtils
{
    /// <summary>
    /// stringがIsNullOrEmptyかを判定し、trueの場合はfallbackに指定されたstringが帰されます。
    /// </summary>
    /// <param name="value">判定したいstring</param>
    /// <param name="fallback">IsNullOrEmpty時にフォールバックするstring</param>
    /// <returns></returns>
    public static string OrDefault(this string value, string fallback)
    {
        return string.IsNullOrEmpty(value) ? fallback : value;
    }
    
    /// <summary>
    /// UnityのRichTextタグ（例：color, size, bold, italicなど）を除去して、平らなテキストを返します。
    /// </summary>
    public static string RemoveUnityRichTextTag(this string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        
        // <...> 形式のタグを除去（ネストされていないことを仮定）
        // UnityのRichTextタグは <tag> または <tag=value> 形式
        return Regex.Replace(text, "<[^>]*>", "");
    }
    
    public static string InsertLineBreaks(string input, int maxCharsPerLine)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        if (maxCharsPerLine <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCharsPerLine));

        var sb = new StringBuilder(input.Length + input.Length / maxCharsPerLine);
        int count = 0;

        foreach (char c in input)
        {
            if (c == '\r')
                continue;

            if (c == '\n')
            {
                sb.AppendLine();
                count = 0;
                continue;
            }

            sb.Append(c);
            count++;

            if (count >= maxCharsPerLine)
            {
                sb.AppendLine();
                count = 0;
            }
        }

        return sb.ToString().TrimEnd('\r', '\n');
    }
    
    public static string ToRandomRichTextColors(string input, int seed = 0)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var rng = seed == 0 ? new System.Random() : new System.Random(seed);
        var sb = new StringBuilder(input.Length * 20);

        foreach (char c in input)
        {
            if (c == '\n')
            {
                sb.Append('\n');
                continue;
            }

            if (c == '\r')
                continue;

            Color color = new Color(
                (float)rng.NextDouble(),
                (float)rng.NextDouble(),
                (float)rng.NextDouble(),
                1f
            );

            string hex = ColorUtility.ToHtmlStringRGBA(color);
            sb.Append("<color=#");
            sb.Append(hex);
            sb.Append('>');
            sb.Append(c);
            sb.Append("</color>");
        }

        return sb.ToString();
    }
}