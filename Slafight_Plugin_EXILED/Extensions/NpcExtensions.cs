using CentralAuth;
using Exiled.API.Features;
using Mirror;

namespace Slafight_Plugin_EXILED.Extensions;

public static class NpcExtensions
{
    public static void HideNpcFromClientPlayerList(this Npc? npc, string source)
    {
        var hub = npc?.ReferenceHub;
        var auth = hub?.authManager;
        if (npc == null || hub == null || auth == null)
            return;

        npc.ReferenceHub.authManager.NetworkSyncedUserId = "ID_Dedicated";
        npc.ReferenceHub.authManager.syncMode = (SyncMode)ClientInstanceMode.DedicatedServer;

        Log.Debug($"[TeamNpc] Hidden from client player list ({source}): {npc.Nickname} Id={npc.Id} NetId={hub.netId} ServerMode={auth.InstanceMode} HubIsHost={hub.IsHost}");
    }
}