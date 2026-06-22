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

        // Dedicated mode keeps the NPC out of player-facing systems and client player lists.
        // HID turret aiming writes directly to the FPC mouse-look state.
        auth.NetworkSyncedUserId = "ID_Dedicated";
        auth.syncMode = (SyncMode)ClientInstanceMode.DedicatedServer;

        Log.Debug($"[Npc] Hidden from client player list ({source}): {npc.Nickname} Id={npc.Id} NetId={hub.netId} ServerMode={auth.InstanceMode}");
    }
}
