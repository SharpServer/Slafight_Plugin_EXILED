using Exiled.Events.EventArgs.Scp173;
using Exiled.Events.Handlers;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.Changes;

public class ScpTeamChanges : IBootstrapHandler
{
    public static void Register()
    {
        Scp173.AddingObserver += OnAddingObserver;
    }

    public static void Unregister()
    {
        Scp173.AddingObserver -= OnAddingObserver;
    }

    private static void OnAddingObserver(AddingObserverEventArgs ev)
    {
        if (ev.Observer.GetTeam() is CTeam.SCPs && ev.Player.IsVanillaOrCustom(RoleTypeId.Scp173, CRoleTypeId.Scp173))
        {
            ev.IsAllowed = false;
        }
    }
}