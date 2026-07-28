using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Features;

namespace Slafight_Plugin_EXILED.Extensions;

public static class PlayerSafetyExtensions
{
    public static bool IsSafePlayer(this Player? player)
    {
        try
        {
            if (player?.ReferenceHub is not { } hub)
                return false;

            if (player.IsHost || hub.IsHost || InternalNpcRegistry.IsManaged(player.Id))
                return false;

            return player.IsNPC || player.IsVerified;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsHidTurretNpc(this Player? player)
        => player is not null &&
           InternalNpcRegistry.IsCategory(player.Id, InternalNpcCategory.HidTurret);
}
