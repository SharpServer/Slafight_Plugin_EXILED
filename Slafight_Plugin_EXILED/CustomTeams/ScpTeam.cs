using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.CustomTeams;

public sealed class ScpTeam : CustomTeam
{
    public override string Name => "SCP";
    public override string Color => ServerColors.Red;
    public override string CassieName => "SCP";
    public override VictoryCondition Victory => VictoryCondition.LastStanding(40);

    protected override bool IncludesVanilla(Player player)
    {
        return player.Role.Type is
            RoleTypeId.Scp173 or
            RoleTypeId.Scp049 or
            RoleTypeId.Scp079 or
            RoleTypeId.Scp096 or
            RoleTypeId.Scp106 or
            RoleTypeId.Scp0492 or
            RoleTypeId.Scp939 or
            RoleTypeId.Scp3114;
    }
}
