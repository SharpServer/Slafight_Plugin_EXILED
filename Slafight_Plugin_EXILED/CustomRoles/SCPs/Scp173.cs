using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.CustomTeams;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

public class Scp173 : CustomRole
{
    public override string Name => "SCP-173";
    public override string Description => "瞬きした相手に近づき、首をへし折る彫刻。";
    public override string Objective => "人間の脅威を容赦なく排除せよ。";
    public override string CustomInfo => Name;
    public override CustomTeam Team => CustomTeam.Get<ScpTeam>();
    public override RoleTypeId BaseRole => RoleTypeId.Scp173;
}
