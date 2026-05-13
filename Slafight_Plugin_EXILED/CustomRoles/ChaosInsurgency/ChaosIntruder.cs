using Exiled.API.Enums;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.ChaosInsurgency;

public class ChaosIntruder : CRole
{
    protected override string RoleName { get; set; } = "カオス・インサージェンシー 侵入兵";
    protected override string Description { get; set; } = "施設に侵入した小規模部隊。警備隊の壊滅及び仲間の脱出を目指せ！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.ChaosIntruder;
    protected override CTeam Team { get; set; } = CTeam.ChaosInsurgency;
    protected override string UniqueRoleKey { get; set; } = "ChaosIntruder";

    public override void SpawnRole(Player? player, RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.ChaosMarauder);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 100;
        player.Health = player.MaxHealth;
        
        player.ClearInventory();
        player.AddItem(ItemType.GrenadeFlash);
        player.AddItem(ItemType.Medkit);
        player.AddItem(ItemType.Adrenaline);
        player.AddItem(ItemType.ArmorCombat);
        player.GiveCItem<GunSuppressiver>();
        player.GiveCItem<KeycardChaosIntruder>();
        
        player.SetAmmo(AmmoType.Nato9, 100);

        player.Position = MapFlags.FirstTeamSpawnPoint;
            
        player.SetCustomInfo("Chaos Insurgency Intruder");
    }
}