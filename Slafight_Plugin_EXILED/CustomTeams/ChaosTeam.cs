using Exiled.API.Features;
using System.Collections.Generic;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.CustomTeams;

public sealed class ChaosTeam : CustomTeam
{
    public override string Name => "カオス・インサージェンシー";
    public override string Color => ServerColors.Green;
    public override string CassieName => "Chaos Insurgency";
    public override VictoryCondition Victory => VictoryCondition.LastStanding(60);
    public override IReadOnlyList<CustomTeam> Allies =>
    [
        CustomTeam.Get<ClassDTeam>()
    ];
    protected override bool IncludesVanilla(Player player) => player.Role.Type is
        RoleTypeId.ChaosConscript or RoleTypeId.ChaosRifleman or RoleTypeId.ChaosRepressor;
}
