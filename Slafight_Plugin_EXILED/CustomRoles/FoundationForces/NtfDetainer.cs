using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class NtfDetainer : CRole
{
    protected override string RoleName { get; set; } = "九尾狐 拘留兵";
    protected override string Description { get; set; } = "SCiPの行動阻害に特化したNTF特技兵。\nXE-11 ANOMALY DETAINERで対象の逃走を防ぐ。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.NtfDetainer;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "NtfDetainer";

    public override void SpawnRole(Player? player, RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.NtfSergeant);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 100;
        player.Health = player.MaxHealth;
        player.ClearInventory();
        player.GiveCItem<GunAnomalyDetainer>();
        player.AddItem(ItemType.GunFSP9);
        player.AddItem(ItemType.KeycardMTFOperative);
        player.AddItem(ItemType.ArmorCombat);
        player.AddItem(ItemType.Medkit);
        player.AddItem(ItemType.Radio);
        player.AddItem(ItemType.Flashlight);

        player.SetAmmo(AmmoType.Nato556, 90);
        player.SetAmmo(AmmoType.Nato9, 120);

        player.SetCustomInfo("Nine-tailed Fox Detainer");
    }
}
