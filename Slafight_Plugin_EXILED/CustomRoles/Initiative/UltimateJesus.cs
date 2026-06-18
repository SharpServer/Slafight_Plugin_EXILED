using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;

namespace Slafight_Plugin_EXILED.CustomRoles.Initiative;

public class UltimateJesus : CRole
{
    protected override string RoleName { get; set; } = "Horizon Initiative Ultimate Jesus";
    protected override string Description { get; set; } = "Xx_ULTIMATE_xX";
    protected override string UniqueRoleKey { get; set; } = "InitiativeUltimateJesus";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.InitiativeUltimateJesus;
    protected override CTeam Team { get; set; } = CTeam.Initiative;
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfPrivate;
    public override bool CanUseProximityChat => true;
    
    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        player.Role.Set(RoleTypeId.Scp049, RoleSpawnFlags.AssignInventory);
        AssignIdentity(player);
        base.OnRoleSpawned(player, roleSpawnFlags);
    }
}