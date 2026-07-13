using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Features;

namespace Slafight_Plugin_EXILED.Extensions;

public static class NpcExtensions
{
    public static void HideNpcFromClientPlayerList(this Npc? npc, string source, bool isSpectatable = false)
    {
        var hub = npc?.ReferenceHub;
        if (npc == null || hub == null)
            return;
        npc.IsSpectatable = isSpectatable;
        hub.serverRoles.NetworkHideFromPlayerList = true;
        Log.Debug($"[Npc] Hidden from client player list ({source}): {npc.Nickname} Id={npc.Id} NetId={hub.netId} ServerMode={hub.authManager.InstanceMode}");
    }

    public static bool IsSafePlayer(this Player? player)
    {
        if (player is null) return false;
        return player.IsNotHost();
    }

    /// <summary>
    /// PlayerがIsHost級ではないかどうかを判定します。
    /// </summary>
    /// <param name="player"></param>
    /// <returns>Player, ReferenceHubのIsHost及びその他の内部管理Npcでないかどうかを返します。</returns>
    public static bool IsNotHost(this Player? player)
    {
        if (player is null) return false;
        return !player.IsHost && !player.ReferenceHub.IsHost && !InternalNpcRegistry.IsManaged(player.Id);
    }

    public static bool IsHidTurretNpc(this Player? player)
    {
        if (player is null) return false;
        return InternalNpcRegistry.IsCategory(player.Id, InternalNpcCategory.HidTurret);
    }
}
