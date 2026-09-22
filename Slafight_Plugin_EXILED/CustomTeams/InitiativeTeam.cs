using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.CustomTeams;

public sealed class InitiativeTeam : CustomTeam
{
    public override string Name => "境界線イニシアチブ";
    public override string Color => ServerColors.BlueGreen;
    public override string CassieName => "X Power Forces";
    public override bool UsesVanillaEnding => false;
    public override VictoryCondition Victory => VictoryCondition.LastStanding(10);
    protected override bool IncludesVanilla(Player player) => false;
}
