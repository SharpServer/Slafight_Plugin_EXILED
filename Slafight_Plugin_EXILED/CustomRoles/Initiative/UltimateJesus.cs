using Exiled.API.Features;
using PlayerRoles;
using ProjectMER.Features.Extensions;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.MainHandlers;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.Initiative;

public class UltimateJesus : CRole
{
    protected override string RoleName { get; set; } = "Horizon Initiative Ultimate Jesus";
    protected override string Description { get; set; } = "Xx_ULTIMATE_xX";
    protected override string? SpawnCustomInfo => $"<color={ServerColors.BlueGreen}>Horizon Initiative Ultimate Jesus</color>";
    protected override string UniqueRoleKey { get; set; } = "InitiativeUltimateJesus";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.InitiativeUltimateJesus;
    protected override CTeam Team { get; set; } = CTeam.Initiative;
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfPrivate;
    public override bool CanUseProximityChat => true;
    
    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        player.Role.Set(RoleTypeId.Scp049, RoleSpawnFlags.AssignInventory);
        AssignIdentity(player);
        player.TryWear("SCP035", player.Transform, out var schematicObject, (Vector3.forward * 0.05f)+(Vector3.up*0.65f));
        schematicObject.Scale *= 1.285f;
        LabApi.Features.Wrappers.Player.Get(player.NetId)!.DestroySchematic(schematicObject);
        base.OnRoleSpawned(player, roleSpawnFlags);
    }
}