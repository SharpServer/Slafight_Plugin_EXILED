using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.ProximityChat;
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

    public override void SpawnRole(Player? player, RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);

        player!.Role.Set(RoleTypeId.Scp0492, RoleSpawnFlags.AssignInventory);
        player.MaxHealth = 125f;
        player.Health = player.MaxHealth;
        player.UniqueRole = UniqueRoleKey;
        player.ClearInventory();
        player.SetCustomInfo("<color=#C50000>Zombified Subject</color>");

        if (player.CurrentRoom is null)
        {
            player.Position = Room.Random(ZoneType.HeavyContainment).WorldPosition(Vector3.up*1.05f);
        }
        else
        {
            player.Position += Vector3.up * 0.85f;
        }
        if (!Handler.CanUsePlayers.Contains(player))
        {
            Handler.CanUsePlayers.Add(player);
        }

        if (!Handler.ActivatedPlayers.Contains(player))
        {
            Handler.ActivatedPlayers.Add(player);
        }
    }
}