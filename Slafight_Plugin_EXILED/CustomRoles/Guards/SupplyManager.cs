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

namespace Slafight_Plugin_EXILED.CustomRoles.Guards;

public class SupplyManager : CRole
{
    protected override string RoleName { get; set; } = "施設供給管理官";
    protected override string Description { get; set; } = "施設の備品等の搬出入などを管理している職員。\n" +
                                                          "施設内に向かい警備員たちと合流せよ。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.SupplyManager;
    protected override CTeam Team { get; set; } = CTeam.Guards;
    protected override string UniqueRoleKey { get; set; } = "SupplyManager";

    public override void SpawnRole(Player? player,RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.FacilityGuard);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 100;
        player.Health = player.MaxHealth;
        player.ClearInventory();
        player.AddItem(ItemType.Medkit);
        player.AddItem(ItemType.Medkit);
        player.AddItem(ItemType.ArmorLight);
        player.AddItem(ItemType.Radio);
        CItem.Get<KeycardSupplyManager>()?.Give(player);
        player.AddItem(ItemType.GunCOM18);
        player.SetAmmo(AmmoType.Nato9,80);
            
        player.SetCustomInfo("Supply Manager");
        Timing.CallDelayed(0.05f, () =>
        {
            if (Random.Range(0, 2) == 0)
            {
                player.Position = MapFlags.SupplyManagerSpawnPointA;
            }
            else
            {
                player.Position = MapFlags.SupplyManagerSpawnPointB;
            }
        });
    }
}