using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.CustomRoles.ChaosInsurgency;

public class ChaosSignal : CRole
{
    protected override string RoleName { get; set; } = "カオス・インサージェンシー 通信兵";
    protected override string Description { get; set; } = "S-Nav 300を用いてユニークな部屋を捜索する。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.ChaosSignal;
    protected override CTeam Team { get; set; } = CTeam.ChaosInsurgency;
    protected override string UniqueRoleKey { get; set; } = "ChaosSignal";

    public override void SpawnRole(Player? player, RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.ChaosRifleman);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 100;
        player.Health = player.MaxHealth;
        
        player.ClearInventory();
        player.SetCategoryLimit(ItemCategory.Radio, 2);
        player.AddItem(ItemType.KeycardChaosInsurgency);
        player.AddItem(ItemType.Medkit);
        player.AddItem(ItemType.Painkillers);
        player.AddItem(ItemType.ArmorCombat);
        player.AddItem(ItemType.GunAK);
        CItem.Get<SNAV300>()?.Give(player);
        
        player.SetAmmo(AmmoType.Nato762, 150);
            
        player.SetCustomInfo("Chaos Insurgency Signal");
    }
}