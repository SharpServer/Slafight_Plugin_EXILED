using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.Fifthist;

public class FifthistMarionetteRole : CRole
{
    protected override string RoleName { get; set; } = "Fifthist Marionette";
    protected override string Description { get; set; } = "ピンクの光によって作り替えられてしまった人間の成れの果て。\n第五教会に従い、生存者どもを騙しながら第五しろ！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.FifthistMarionette;
    protected override CTeam Team { get; set; } = CTeam.Fifthists;
    protected override string UniqueRoleKey { get; set; } = "FifthistMarionette";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Scp0492;
    protected override RoleSpawnFlags? SpawnBaseRoleFlags => RoleSpawnFlags.AssignInventory;
    protected override float? SpawnMaxHealth => 100f;
    protected override bool SpawnClearsInventory => true;
    protected override string SpawnCustomInfo => "<color=#FF0090>Fifthist Marionette</color>";
    public override bool CanUseProximityChat => true;
    public override bool ProximityChatEnabledByDefault => true;

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        var scp3125 = Player.List.FirstOrDefault(p => p.GetCustomRole() is CRoleTypeId.Scp3125);
        if (scp3125 is not null)
        {
            TrySetPlayerPosition(player, scp3125.Position + Vector3.up * 0.15f, nameof(FifthistMarionetteRole));
        }
        else if (player.CurrentRoom is null)
        {
            TrySetPlayerPosition(
                player,
                Room.Random(ZoneType.HeavyContainment).WorldPosition(Vector3.up * 1.05f),
                nameof(FifthistMarionetteRole));
        }
        else
        {
            TrySetPlayerPosition(player, player.Position + Vector3.up * 0.85f, nameof(FifthistMarionetteRole));
        }
    }
}
