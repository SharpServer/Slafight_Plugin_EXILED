using System.Text.RegularExpressions;

namespace Slafight_Plugin_EXILED.Extensions;

public static class StringExtensions
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
}