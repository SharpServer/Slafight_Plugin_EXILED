using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Features;

public static class RoleSyncReadiness
{
    private static readonly Dictionary<uint, float> LastSelfRoleSyncSentAt = [];

    public static void MarkSelfRoleSyncSent(ReferenceHub hub)
    {
        if (!TryGetNetId(hub, out var netId))
            return;

        LastSelfRoleSyncSentAt[netId] = Time.realtimeSinceStartup;
    }

    public static bool IsSelfRoleSyncSettled(ReferenceHub hub, float settleSeconds)
    {
        if (!TryGetNetId(hub, out var netId))
            return false;

        if (!LastSelfRoleSyncSentAt.TryGetValue(netId, out var sentAt))
            return false;

        return Time.realtimeSinceStartup - sentAt >= settleSeconds;
    }

    public static void Clear()
    {
        LastSelfRoleSyncSentAt.Clear();
    }

    private static bool TryGetNetId(ReferenceHub hub, out uint netId)
    {
        netId = 0;

        try
        {
            if (hub == null)
                return false;

            netId = ((NetworkBehaviour)hub).netId;
            return netId != 0;
        }
        catch
        {
            return false;
        }
    }
}
