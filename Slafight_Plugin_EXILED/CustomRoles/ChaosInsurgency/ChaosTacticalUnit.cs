using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.CustomRoles.ChaosInsurgency;

public class ChaosTacticalUnit : CRole
{
    protected override string RoleName { get; set; } = "カオス・インサージェンシー 戦術兵";
    protected override string Description { get; set; } = "特殊なリボルバーを用いて邪魔者を排除せよ！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.ChaosTacticalUnit;
    protected override CTeam Team { get; set; } = CTeam.ChaosInsurgency;
    protected override string UniqueRoleKey { get; set; } = "ChaosTacticalUnit";

    public override void SpawnRole(Player? player, RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.ChaosMarauder);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 100;
        player.Health = player.MaxHealth;
        
        player.ClearInventory();
        CItem.Get<GunTacticalRevolver>()?.Give(player);
        player.AddItem(ItemType.KeycardChaosInsurgency);
        player.AddItem(ItemType.Medkit);
        player.AddItem(ItemType.Painkillers);
        player.AddItem(ItemType.ArmorCombat);
        player.AddItem(ItemType.GrenadeFlash);
        
        player.SetAmmo(AmmoType.Ammo44Cal, 40);
            
        player.SetCustomInfo("Chaos Insurgency Tactical Unit");
    }
}