using System.Globalization;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Extensions;

public static class ColorExtensions
{
    public static Color ParseHtmlToColor(string colorCode)
    {
        var isSuccessResult = ColorUtility.TryParseHtmlString(colorCode, out var color);
        return isSuccessResult ? color : Color.white;
    }
    
    /// <summary>
    /// Color32をHTMLのHEXカラーコード（#RRGGBB）に変換します
    /// </summary>
    public static string ToHex(this Color32 color)
    {
        return $"#{color.r:X2}{color.g:X2}{color.b:X2}";
    }

    /// <summary>
    /// Color32をHTMLのHEXカラーコード（#RRGGBBAA）にアルファ込みで変換します
    /// </summary>
    public static string ToHexWithAlpha(this Color32 color)
    {
        return $"#{color.r:X2}{color.g:X2}{color.b:X2}{color.a:X2}";
    }

    /// <summary>
    /// HTMLのHEXカラーコード（#RRGGBBまたは#RRGGBBAA）をColor32に変換します
    /// </summary>
    public static Color32 FromHex(this string hex)
    {
        if (string.IsNullOrEmpty(hex))
            return Color.clear;

        // #を除去
        hex = hex.TrimStart('#');

        byte r, g, b, a = 255;

        if (hex.Length == 6)
        {
            r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
            g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
            b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
        }
        else if (hex.Length == 8)
        {
            r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
            g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
            b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
            a = byte.Parse(hex.Substring(6, 2), NumberStyles.HexNumber);
        }
        else
        {
            return Color.clear;
        }

        return new Color32(r, g, b, a);
    }
}