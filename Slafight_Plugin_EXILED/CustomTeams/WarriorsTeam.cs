using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.CustomTeams;

public sealed class WarriorsTeam : CustomTeam
{
    public override string Name => "戦士達";
    public override string CassieName => "Warriors";
    protected override bool IncludesVanilla(Player player) => false;
}
