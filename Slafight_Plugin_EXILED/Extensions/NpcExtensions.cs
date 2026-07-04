using CentralAuth;
using Exiled.API.Features;
using Mirror;
using Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

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

    /// <summary>
    /// PlayerがIsHost級ではないかどうかを判定します。
    /// </summary>
    /// <param name="player"></param>
    /// <returns>Player, ReferenceHubのIsHost及びその他の特殊NPCでないかどうかを返します。</returns>
    public static bool IsNotHost(this Player player)
    {
        if (player is null) return false;
        return !player.IsHost && !player.ReferenceHub.IsHost && !player.IsHidTurretNpc();
    }

    public static bool IsHidTurretNpc(this Player player)
    {
        if (player is null) return false;
        return HIDTurretObject.PublicTurretNpcIds.Contains(player.Id);
    }
}
