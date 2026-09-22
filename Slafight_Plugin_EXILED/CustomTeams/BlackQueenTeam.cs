using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.CustomTeams;

public sealed class BlackQueenTeam : CustomTeam
{
    public override string Name => "黒の女王";
    public override string Color => ServerColors.Black;
    public override string CassieName => "Black Q been";
    protected override bool IncludesVanilla(Player player) => false;
}
