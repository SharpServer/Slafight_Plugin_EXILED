using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.CustomTeams;

public sealed class NullTeam : CustomTeam
{
    public override string Name => "不明な勢力";
    public override string CassieName => "Unknown Forces";
    protected override bool IncludesVanilla(Player player) => false;
}
