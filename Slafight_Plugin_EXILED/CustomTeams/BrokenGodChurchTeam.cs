using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.CustomTeams;

public sealed class BrokenGodChurchTeam : CustomTeam
{
    public override string Name => "壊れた神の教会";
    public override string CassieName => "Black God Charge";
    protected override bool IncludesVanilla(Player player) => false;
}
