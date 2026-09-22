using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.CustomTeams;

public sealed class SarkicTeam : CustomTeam
{
    public override string Name => "サーキック・カルト";
    public override string CassieName => "SAW KEY CARD";
    protected override bool IncludesVanilla(Player player) => false;
}
