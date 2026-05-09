using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.Scientist;

public class Engineer : CRole
{
    protected override string RoleName { get; set; } = "エンジニア";
    protected override string Description { get; set; } = "施設内の様々なシステム等を整備する職員。\n" +
                                                          "Toolboxを用いてSCPを食い止めたり、他職員の脱出等をサポートせよ。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Engineer;
    protected override CTeam Team { get; set; } = CTeam.Scientists;
    protected override string UniqueRoleKey { get; set; } = "Engineer";

    public override void SpawnRole(Player? player, RoleSpawnFlags flags = RoleSpawnFlags.All)
    {
        if (player == null) return;
        player.Role.Set(RoleTypeId.Scientist);
        base.SpawnRole(player, flags);

        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 100;
        player.Health = player.MaxHealth;

        player.ClearInventory();
        player.AddItem(ItemType.KeycardContainmentEngineer);
        player.AddItem(ItemType.Medkit);
        player.AddItem(ItemType.Medkit);
        CItem.Get<Toolbox>()?.Give(player);

        var room = Room.Get(RoomType.HczTestRoom);
        var pos = room != null ? room.WorldPosition(new Vector3(0f, 1f, 0f)) : player.Position;
        player.Position = pos;

        player.SetCustomInfo("Engineer");
    }
}