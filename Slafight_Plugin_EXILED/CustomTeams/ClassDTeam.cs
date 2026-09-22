using Exiled.API.Features;
using System.Collections.Generic;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.CustomRoles.FoundationForces.MTFs.Epsilon11;

namespace Slafight_Plugin_EXILED.CustomTeams;

public sealed class ClassDTeam : CustomTeam
{
    public override string Name => "Dクラス職員";
    public override string Color => ServerColors.Pumpkin;
    public override string CassieName => "Class D Personnel";
    public override IReadOnlyList<CustomTeam> Allies =>
    [
        CustomTeam.Get<ChaosTeam>()
    ];
    protected override bool IncludesVanilla(Player player) => player.Role.Type == RoleTypeId.ClassD;

    public override SpawnSetRoleDefinition? Escape(EscapeContext escape)
    {
        return escape.IsEscortedByAllyOf<FoundationTeam>()
            ? SpawnSetRoleDefinition.Custom<NtfCombatSpecialist>()
            : null;
    }
}
