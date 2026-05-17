using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Roles;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.CustomRoles.Others;

public class HideWatch : CRole
{
    protected override string RoleName { get; set; } = $"<color={ServerColors.Cyan}><b>THE HIDEWATCH</b></color>";
    protected override string Description { get; set; } = "ぐへへへへ";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.HideWatch;
    protected override CTeam Team { get; set; } = CTeam.Others;
    protected override string UniqueRoleKey { get; set; } = "HideWatch";

    public override void SpawnRole(Player? player, RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.Overwatch);
        player.UniqueRole = UniqueRoleKey;

        Timing.CallDelayed(0.05f, () =>
        {
            player.SetCustomInfo($"<color={ServerColors.Cyan}>THE HIDEWATCH</color>");
            player.ChangeAppearance(RoleTypeId.Spectator);
        });
    }
}