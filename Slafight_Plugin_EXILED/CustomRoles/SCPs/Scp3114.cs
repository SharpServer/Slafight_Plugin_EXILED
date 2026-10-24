using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.CustomTeams;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

public class Scp3114 : CustomRole
{
    public override string Name => "SCP-3114";
    public override string CustomInfo => Name;
    public override string Description => "騙し、入り込み、唐突の恐怖を与えよ。";
    public override string Objective => "皮をかぶって集団に潜り、恐怖と壊滅を与えよ。";
    public override CustomTeam Team => CustomTeam.Get<ScpTeam>();
    public override RoleTypeId BaseRole => RoleTypeId.Scp3114;
}