using Exiled.API.Features;
using System.Collections.Generic;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.CustomTeams;

public sealed class GuardTeam : CustomTeam
{
    public override string Name => "警備員";
    public override string Color => ServerColors.Cyan;
    public override string CassieName => "Facility Guard Personnel";
    public override bool IsGroupOfInterest => false;
    public override IReadOnlyList<CustomTeam> Allies =>
    [
        CustomTeam.Get<FoundationTeam>(),
        CustomTeam.Get<ScientistTeam>()
    ];
    protected override bool IncludesVanilla(Player player) => player.Role.Type == RoleTypeId.FacilityGuard;
}
