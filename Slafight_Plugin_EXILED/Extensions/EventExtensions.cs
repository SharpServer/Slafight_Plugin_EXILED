using Exiled.Events.EventArgs.Player;

namespace Slafight_Plugin_EXILED.Extensions;

public static class EventExtensions
{
    // HurtingEventArgs
    public static bool IsDeathExpected(this HurtingEventArgs ev)
    {
        if (!ev.IsAllowed) return false;
        if (ev.Player is null) return false;
        return ev.IsInstantKill || ev.Player.Health - ev.Amount <= 0;
    }
}
