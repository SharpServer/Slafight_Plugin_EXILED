using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.CustomRoles.ChaosInsurgency;

public class ChaosSniper : CRole
{
    protected override string RoleName { get; set; } = "カオス・インサージェンシー 狙撃兵";
    protected override string Description { get; set; } = "スナイパーライフルを用いて素早く対象を制圧する。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.ChaosSniper;
    protected override CTeam Team { get; set; } = CTeam.ChaosInsurgency;
    protected override string UniqueRoleKey { get; set; } = "ChaosSniper";

    public override void SpawnRole(Player? player, RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.ChaosRepressor);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 100;
        player.Health = player.MaxHealth;
        
        player.ClearInventory();
        player.GiveCItem<GunSL8>();
        player.AddItem(ItemType.KeycardChaosInsurgency);
        player.AddItem(ItemType.Medkit);
        player.AddItem(ItemType.Adrenaline);
        player.AddItem(ItemType.ArmorCombat);
        
        player.SetAmmo(AmmoType.Nato556, 100);
            
        player.SetCustomInfo("Chaos Insurgency Sniper");
    }
}