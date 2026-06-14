using Exiled.API.Extensions;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;

namespace Slafight_Plugin_EXILED.CustomRoles.Moderators;

public class HideWatch : CRole
{
    protected override string RoleName { get; set; } = $"<color={ServerColors.Cyan}><b>THE HIDEWATCH</b></color>";
    protected override string Description { get; set; } = "ぐへへへへ";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.HideWatch;
    protected override CTeam Team { get; set; } = CTeam.Moderators;
    protected override string UniqueRoleKey { get; set; } = "HideWatch";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Overwatch;
    protected override string SpawnCustomInfo => $"<color={ServerColors.Cyan}>THE HIDEWATCH</color>";

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        var playerId = player.Id;

        Timing.CallDelayed(RoleSpawnTimings.AfterRoleSet, () =>
        {
            var current = Player.Get(playerId);
            if (!Check(current) || !IsSafeRolePlayer(current))
                return;

            current.ChangeAppearance(RoleTypeId.Spectator);
        });
    }
}
