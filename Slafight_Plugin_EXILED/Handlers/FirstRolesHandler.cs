using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.SpawnSets.RoundStart;
using Slafight_Plugin_EXILED.SpawnSets.Specials;
using PlayerHandlers = Exiled.Events.Handlers.Player;
using ServerHandlers = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.Handlers;

/// <summary>
/// バニラのラウンド開始割り当てを止め、<see cref="SpawnSet"/> で役職を配ります。
/// </summary>
public sealed class FirstRolesHandler : EventHandlerBase
{
    /// <inheritdoc />
    public override void RegisterEvents()
    {
        PlayerHandlers.ChangingRole += CancelVanillaAssignment;
        ServerHandlers.RoundStarted += AssignFirstRoles;
    }

    /// <inheritdoc />
    public override void UnregisterEvents()
    {
        PlayerHandlers.ChangingRole -= CancelVanillaAssignment;
        ServerHandlers.RoundStarted -= AssignFirstRoles;
    }

    /// <summary>
    /// バニラのラウンド開始割り当て (<see cref="SpawnReason.RoundStart"/>) だけを止めます。
    /// </summary>
    private static void CancelVanillaAssignment(ChangingRoleEventArgs ev)
    {
        if (ev.Reason is SpawnReason.RoundStart)
            ev.IsAllowed = false;
    }

    /// <summary>
    /// SCP を先に、残り全員に人間役職を配ります。
    /// </summary>
    private static void AssignFirstRoles()
    {
        new FirstRolesSCPsSet().Spawn();
        new FirstRolesHumanSet().Spawn();
    }
}
