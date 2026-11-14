using System.Linq;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.CustomRoles.Fifthists;
using Slafight_Plugin_EXILED.CustomRoles.FoundationForces.MTFs.Epsilon11;
using Slafight_Plugin_EXILED.CustomRoles.SCPs;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.CustomTeams;

public sealed class FifthistTeam : CustomTeam
{
    public override string Name => "第五教会";
    public override string Color => ServerColors.Magenta;
    public override string CassieName => "$pitch_1.05 5 5 5 $pitch_1 Forces";
    public override bool UsesVanillaEnding => false;
    public override bool ShowsRoster => true;

    /// <summary>SCP-3005 も味方として数え、それ以外の敵対陣営が全滅したら勝ち。</summary>
    public override VictoryCondition Victory => VictoryCondition.Custom(
        team => team.Members.Any() && !Player.List.Any(player =>
            player.IsSafePlayer() && player.IsAlive && !IsRosterMember(player) &&
            Of(player) is { } other && !team.IsSameSide(other)),
        priority: 10);

    public override string Objective => "第五を探し出し、救出し、従い、施設を占領せよ。";

    /// <summary>SCP-3005 か第五教会 司祭が場に居るか。第五教会の波はこの間だけ出ます。</summary>
    public static bool IsBlessed =>
        CustomRole.PlayersOf<Scp3005>().Any(player => player.IsAlive) ||
        CustomRole.PlayersOf<FifthistPriest>().Any(player => player.IsAlive);

    public override bool IsRosterMember(Player player) => base.IsRosterMember(player) || CustomRole.Is<Scp3005>(player);

    public override SpawnSetRoleDefinition? Escape(EscapeContext escape)
    {
        if (escape.IsEscortedByAllyOf<ChaosTeam>())
            return SpawnSetRoleDefinition.Vanilla(RoleTypeId.ChaosConscript);

        if (escape.IsEscortedByAllyOf<FoundationTeam>())
            return SpawnSetRoleDefinition.Custom<NtfPrivate>();

        return null;
    }

    protected override bool IncludesVanilla(Player player) => false;
}
