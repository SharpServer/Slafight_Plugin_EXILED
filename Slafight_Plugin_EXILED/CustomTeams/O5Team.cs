using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.CustomTeams;

public sealed class O5Team : CustomTeam
{
    public override string Name => "O5評議会";
    public override string Color => ServerColors.Black;
    public override string CassieName => "O5 Command";
    protected override bool IncludesVanilla(Player player) => false;
}
