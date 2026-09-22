using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.CustomTeams;

public sealed class GoCTeam : CustomTeam
{
    public override string Name => "世界オカルト連合";
    public override string Color => "#0000C8";
    public override string CassieName => "G O C";
    public override bool UsesVanillaEnding => false;
    public override VictoryCondition Victory => VictoryCondition.LastStanding(30);
    protected override bool IncludesVanilla(Player player) => false;
}
