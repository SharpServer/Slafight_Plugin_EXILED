using Exiled.Events.EventArgs.Player;

namespace Slafight_Plugin_EXILED.Extensions;

public static class EventExtensions
{
    // HurtingEventArgs
    public static bool IsDeathExpected(this HurtingEventArgs ev)
    {
        if (ev.Player is null) return false;
        if (ev.Player.Health - ev.Amount <= 0)
        {
            return true;
        }
        return false;
    }
}