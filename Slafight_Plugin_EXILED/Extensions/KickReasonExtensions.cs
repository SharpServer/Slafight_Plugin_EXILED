using System;
using System.Reflection;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features.Attributes;

namespace Slafight_Plugin_EXILED.Extensions;

public static class KickReasonExtensions
{
    private const string Prefix = "You have been kicked. Reason: ";

    public static bool TryParseKickReason(string message, out KickReason reason)
    {
        reason = KickReason.Custom;

        if (string.IsNullOrWhiteSpace(message))
            return false;

        if (!message.StartsWith(Prefix, StringComparison.Ordinal))
            return false;

        var text = message.Substring(Prefix.Length).Trim();

        foreach (KickReason item in Enum.GetValues(typeof(KickReason)))
        {
            var field = typeof(KickReason).GetField(item.ToString());
            var attr = field?.GetCustomAttribute<ReasonAttribute>();

            if (attr != null && string.Equals(attr.Reason, text, StringComparison.OrdinalIgnoreCase))
            {
                reason = item;
                return true;
            }
        }

        return false;
    }
}