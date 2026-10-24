using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.CustomTeams;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

public class Scp939 : CustomRole
{
    public override string Name => "SCP-939";
    public override string CustomInfo => Name;
    public override string Description => "切り裂き、相手の声を自らの武器としろ。";
    public override string Objective => "悲鳴を集め、おびき寄せ、敵を壊滅へと追い込め。";
    public override CustomTeam Team => CustomTeam.Get<ScpTeam>();
    public override RoleTypeId BaseRole => RoleTypeId.Scp939;
}