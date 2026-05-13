using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.Guards;

public class SecurityTeamGuard : CRole
{
    protected override string RoleName { get; set; } = "保安部隊員";
    protected override string Description { get; set; } = "職員たちを保護し、脱出を助ける。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.SecurityTeamGuard;
    protected override CTeam Team { get; set; } = CTeam.Guards;
    protected override string UniqueRoleKey { get; set; } = "SecurityTeamGuard";

    public override void SpawnRole(Player? player,RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.FacilityGuard);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 100;
        player.Health = player.MaxHealth;
        player.ClearInventory();
        player.GiveCItem<GunFSP18>();
        player.AddItem(ItemType.KeycardGuard);
        player.AddItem(ItemType.Medkit);
        player.AddItem(ItemType.Painkillers);
        player.AddItem(ItemType.ArmorCombat);
        player.AddItem(ItemType.Radio);
        player.SetAmmo(AmmoType.Nato9,110);

        player.Position = MapFlags.FirstTeamSpawnPoint;
            
        player.SetCustomInfo("Security Team Guard");
    }
}