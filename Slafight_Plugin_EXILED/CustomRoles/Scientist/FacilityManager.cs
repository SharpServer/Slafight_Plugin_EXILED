using Exiled.API.Features;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.CustomRoles.Scientist;

public class FacilityManager : CRole
{
    protected override string RoleName { get; set; } = "<color=#dc143c>施設管理官</color>";
    protected override string Description { get; set; } = "施設を管理・運営する重要な科学者。\n" +
                                                          "区画管理官や警備員たちに指示を出し、収容違反に対処する。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.FacilityManager;
    protected override CTeam Team { get; set; } = CTeam.Scientists;
    protected override string UniqueRoleKey { get; set; } = "FacilityManager";

    public override void SpawnRole(Player? player,RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.Scientist);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 100;
        player.Health = player.MaxHealth;
        player.ClearInventory();
        Log.Debug("Giving Items to FacilityManager");
        player.AddItem(ItemType.GunCrossvec);
        player.AddItem(ItemType.KeycardFacilityManager);
        player.AddItem(ItemType.Medkit);
        player.AddItem(ItemType.Medkit);
        player.AddItem(ItemType.ArmorCombat);
        player.AddItem(ItemType.Radio);
            
        player.SetCustomInfo("Facility Manager");
        Timing.CallDelayed(0.05f, () =>
        {
            player.Position = MapFlags.FacilityManagerSpawnPoint;
        });
    }
}