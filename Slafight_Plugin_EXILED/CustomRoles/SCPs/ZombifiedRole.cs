using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

public class ZombifiedRole : CRole
{
    protected override string RoleName { get; set; } = "Zombified Subject";
    protected override string Description { get; set; } = "様々な要因によりゾンビと化してしまった人の成れの果て。\n" +
                                                          "暴れまくって施設に混沌をもたらせよ！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Zombified;
    protected override CTeam Team { get; set; } = CTeam.SCPs;
    protected override string UniqueRoleKey { get; set; } = "Zombified";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Scp0492;
    protected override RoleSpawnFlags? SpawnBaseRoleFlags => RoleSpawnFlags.AssignInventory;
    protected override float? SpawnMaxHealth => 125f;
    protected override bool SpawnClearsInventory => true;
    protected override string SpawnCustomInfo => "<color=#C50000>Zombified Subject</color>";
    public override bool CanUseProximityChat => true;
    public override bool ProximityChatEnabledByDefault => true;

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        if (player.CurrentRoom is null)
        {
            TrySetPlayerPosition(
                player,
                Room.Random(ZoneType.HeavyContainment).WorldPosition(Vector3.up * 1.05f),
                nameof(ZombifiedRole));
        }
        else
        {
            TrySetPlayerPosition(player, player.Position + Vector3.up * 0.85f, nameof(ZombifiedRole));
        }
    }
}
