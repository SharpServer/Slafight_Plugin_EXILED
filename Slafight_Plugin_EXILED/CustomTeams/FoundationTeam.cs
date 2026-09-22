using Exiled.API.Features;
using System.Collections.Generic;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.CustomTeams;

public sealed class FoundationTeam : CustomTeam
{
    public override string Name => "機動部隊";
    public override string Color => ServerColors.Cyan;
    public override string CassieName => "MtfUnit";
    public override bool IsGroupOfInterest => false;
    public override VictoryCondition Victory => VictoryCondition.LastStanding(50);
    public override IReadOnlyList<CustomTeam> Allies =>
    [
        CustomTeam.Get<ScientistTeam>(),
        CustomTeam.Get<GuardTeam>()
    ];
    protected override bool IncludesVanilla(Player player) => player.Role.Type is
        RoleTypeId.NtfPrivate or RoleTypeId.NtfSergeant or RoleTypeId.NtfSpecialist or RoleTypeId.NtfCaptain;
}
