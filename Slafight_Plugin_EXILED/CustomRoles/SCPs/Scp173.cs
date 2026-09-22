using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.CustomTeams;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

public class Scp173 : CustomRole
{
    public override string Name => "SCP-173";
    public override CustomTeam Team => CustomTeam.Get<ScpTeam>();
    public override RoleTypeId BaseRole => RoleTypeId.Scp173;
}
