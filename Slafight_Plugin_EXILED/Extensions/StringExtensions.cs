namespace Slafight_Plugin_EXILED.Extensions;

public static class StringExtensions
{
    // string.IsNullOrEmpty かどうかを判定し、trueならfallbackを返す
    public static string OrDefault(this string value, string fallback)
    {
        return string.IsNullOrEmpty(value) ? fallback : value;
    }
}