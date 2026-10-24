using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.CustomTeams;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

public class Scp106 : CustomRole
{
    public override string Name => "SCP-106";
    public override string CustomInfo => Name;
    public override string Description => "ポケットディメンションへ人々を連れて行く老人。\n" +
                                          "狡猾に、欺き、絶望へと陥れろ。";
    public override string Objective => "しつこい追跡を繰り返し、領域へと引きずり込め。";
    public override CustomTeam Team => CustomTeam.Get<ScpTeam>();
    public override RoleTypeId BaseRole => RoleTypeId.Scp106;
}