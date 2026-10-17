using System.Text;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.API.Features;

public static class PluginInfoUtils
{
    public static string GetVersionString(bool ignoreSpecificTags = false, bool clearStyles = false)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append($"v{Plugin.Singleton.Version}");
        foreach (var tag in Plugin.Singleton.SpecificVersionTags)
        {
            sb.Append($"-{tag}");
        }
        string output = sb.ToString();
        if (clearStyles) output = output.RemoveUnityRichTextTag();
        return output;
    }
}