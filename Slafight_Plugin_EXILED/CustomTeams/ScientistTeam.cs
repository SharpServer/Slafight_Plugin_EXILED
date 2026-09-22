using Exiled.API.Features;
using System.Collections.Generic;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.CustomRoles.FoundationForces.MTFs.Epsilon11;

namespace Slafight_Plugin_EXILED.CustomTeams;

public sealed class ScientistTeam : CustomTeam
{
    public override string Name => "科学者";
    public override string Color => ServerColors.Yellow;
    public override string CassieName => "Scientist Personnel";
    public override bool IsGroupOfInterest => false;
    public override IReadOnlyList<CustomTeam> Allies =>
    [
        CustomTeam.Get<FoundationTeam>(),
        CustomTeam.Get<GuardTeam>()
    ];
    protected override bool IncludesVanilla(Player player) => player.Role.Type == RoleTypeId.Scientist;

    public override SpawnSetRoleDefinition? Escape(EscapeContext escape) =>
        SpawnSetRoleDefinition.Custom<NtfContainmentSpecialist>();
}
