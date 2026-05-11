using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class NtfSpecialist : CRole
{
    protected override string RoleName { get; set; } = "九尾狐 スペシャリスト";
    protected override string Description { get; set; } = "九尾狐の中でもとてもオブジェクト達に精通している戦術スペシャリスト。\n専用の対物ライフルでオブジェクトを無力化する。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.NtfSpecialist;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "NtfSpecialist";

    public override void SpawnRole(Player? player,RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.NtfSpecialist);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 100;
        player.Health = player.MaxHealth;
        player.ClearInventory();
        player.GiveCItem<GunM82>();
        player.AddItem(ItemType.GunCOM18);
        player.AddItem(ItemType.KeycardMTFCaptain);
        player.AddItem(ItemType.Medkit);
        player.AddItem(ItemType.Medkit);
        player.AddItem(ItemType.GrenadeHE);
        player.AddItem(ItemType.ArmorHeavy);
        player.AddItem(ItemType.Radio);
        
        player.SetAmmo(AmmoType.Nato556, 180);

        player.SetCustomInfo("Nine-tailed Fox Specialist");
    }
}