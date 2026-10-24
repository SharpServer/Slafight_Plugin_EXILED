using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.CustomTeams;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

public class Scp096 : CustomRole
{
    public override string Name => "SCP-096";
    public override string CustomInfo => Name;
    public override string Description => "暴れ狂い、全てを破壊する。";
    public override string Objective => "顔を視認した相手に怒りのままに代償を払わせよ。";
    public override CustomTeam Team => CustomTeam.Get<ScpTeam>();
    public override RoleTypeId BaseRole => RoleTypeId.Scp096;
}