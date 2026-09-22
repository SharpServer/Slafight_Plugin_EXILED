using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.CustomTeams;

public sealed class UIUTeam : CustomTeam
{
    public override string Name => "連邦捜査局(FBI)異常事件課";
    public override string CassieName => "U I U";
    protected override bool IncludesVanilla(Player player) => false;
}
