using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class NtfGeneral : CRole
{
    protected override string RoleName { get; set; } = "<color=blue>九尾狐 司令官</color>";
    protected override string Description { get; set; } = "Epsilon-11を率いる高位の司令官。\n隊長等と連携し、確実に施設に安定をもたらせ！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.NtfGeneral;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "NtfGeneral";

    public override void SpawnRole(Player? player,RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.NtfCaptain);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 100;
        player.Health = player.MaxHealth;
        player.ClearInventory();
        Log.Debug("Giving Items to NtfGeneral");
        player.AddItem(ItemType.KeycardMTFCaptain);
        CItem.Get<SerumD>()?.Give(player);
        CItem.Get<AdvancedMedkit>()?.Give(player);
        player.AddItem(ItemType.GrenadeHE);
        player.AddItem(ItemType.GrenadeHE);
        player.AddItem(ItemType.Radio);
        CItem.Get<ArmorVip>()?.Give(player);
        CItem.Get<GunFRMGX>()?.Give(player);
            
        player.SetAmmo(AmmoType.Nato556,320);

        //PlayerExtensions.OverrideRoleName(player,$"{player.GroupName}","Hammer Down Commander");
        CustomInfoDisplay.Apply(player, "Nine-tailed Fox General");
    }
}
