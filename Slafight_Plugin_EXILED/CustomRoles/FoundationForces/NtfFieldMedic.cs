using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class NtfFieldMedic : CRole
{
    protected override string RoleName { get; set; } = "NTF FIELD MEDIC";
    protected override string Description { get; set; } =
        "Nine-Tailed-Foxの野戦衛生兵。\n" +
        "S-41 MEDICAL PISTOLで遠距離から味方を治療できる。\n" +
        "敵勢力も回復してしまうため射線に注意。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.NtfFieldMedic;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "NtfFieldMedic";

    public override void SpawnRole(Player? player, RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.NtfPrivate);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 100;
        player.Health = player.MaxHealth;
        player.ClearInventory();

        player.AddItem(ItemType.GunCrossvec);
        player.GiveCItem<S41MedicalPistol>();
        player.AddItem(ItemType.KeycardMTFOperative);
        player.AddItem(ItemType.ArmorCombat);
        player.GiveCItem<AdvancedMedkit>();
        player.AddItem(ItemType.Radio);
        player.AddItem(ItemType.Flashlight);

        player.SetAmmo(AmmoType.Nato9, 120);

        CustomInfoDisplay.Apply(player, "Nine-tailed Fox Field Medic");
    }
}
