using Exiled.API.Features;

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

}
