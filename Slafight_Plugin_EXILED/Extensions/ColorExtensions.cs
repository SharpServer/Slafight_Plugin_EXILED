using UnityEngine;

namespace Slafight_Plugin_EXILED.Extensions;

public class ColorExtensions
{
    public static Color ParseHtmlToColor(string colorCode)
    {
        var isSuccessResult = ColorUtility.TryParseHtmlString(colorCode, out var color);
        return isSuccessResult ? color : Color.white;
    }
}