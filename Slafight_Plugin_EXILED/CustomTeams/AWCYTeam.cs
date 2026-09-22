using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.CustomTeams;

public sealed class AWCYTeam : CustomTeam
{
    public override string Name => "Are We Cool Yet?";
    public override string CassieName => "Are were code yet";
    protected override bool IncludesVanilla(Player player) => false;
}
