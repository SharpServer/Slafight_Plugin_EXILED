using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.CustomTeams;

public sealed class ModeratorsTeam : CustomTeam
{
    public override string Name => "管理者";
    public override string Color => "#C0C0C0";
    public override string CassieName => "Moderators";
    protected override bool IncludesVanilla(Player player) => false;
}
