using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.CustomTeams;

public sealed class SerpentsHandTeam : CustomTeam
{
    public override string Name => "サーペント・ハンド";
    public override string CassieName => "Serpents Hand";
    protected override bool IncludesVanilla(Player player) => false;
}
