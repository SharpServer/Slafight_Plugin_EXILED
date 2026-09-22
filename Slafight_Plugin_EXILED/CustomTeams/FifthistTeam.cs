using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.CustomTeams;

public sealed class FifthistTeam : CustomTeam
{
    public override string Name => "第五教会";
    public override string Color => ServerColors.Magenta;
    public override string CassieName => "$pitch_1.05 5 5 5 $pitch_1 Forces";
    public override bool UsesVanillaEnding => false;
    public override VictoryCondition Victory => VictoryCondition.LastStanding(10);
    protected override bool IncludesVanilla(Player player) => false;
}
