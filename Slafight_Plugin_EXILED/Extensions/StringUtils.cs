using System;
using System.Text;
using UnityEngine;
using Random = System.Random;

namespace Slafight_Plugin_EXILED.Extensions;

public static class StringUtils
{
    /// <summary>
    /// null または空なら <paramref name="fallback"/> を返します。
    /// </summary>
    public static string OrDefault(this string value, string fallback)
    {
        return string.IsNullOrEmpty(value) ? fallback : value;
    }
    
    public static string RemoveUnityRichTextTag(this string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var result = new StringBuilder(text.Length);
        bool inTag = false;

        foreach (char c in text)
        {
            if (c == '<')
            {
                inTag = true;
                continue;
            }

            if (inTag)
            {
                if (c == '>')
                    inTag = false;

                continue;
            }

            result.Append(c);
        }

        return result.ToString();
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

        var rng = seed == 0 ? new Random() : new Random(seed);
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