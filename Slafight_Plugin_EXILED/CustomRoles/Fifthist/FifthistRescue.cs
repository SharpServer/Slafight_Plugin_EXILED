using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.Fifthist;

public class FifthistRescure : CRole
{
    protected override string RoleName { get; set; } = "第五教会 救出師";
    protected override string Description { get; set; } = "非常に<color=#ff00fa>第五的</color>な存在を脱出させなければいけない";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.FifthistRescure;
    protected override CTeam Team { get; set; } = CTeam.Fifthists;
    protected override string UniqueRoleKey { get; set; } = "FIFTHIST";

    public override void SpawnRole(Player? player, RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);

        player!.Role.Set(RoleTypeId.Tutorial);
        int maxHealth = 150;

        player.UniqueRole = UniqueRoleKey;
        player.CustomInfo = "<color=#FF0090>Fifthist Rescure</color>";
        player.InfoArea |= PlayerInfoArea.Nickname;
        player.InfoArea &= ~PlayerInfoArea.Role;
        player.MaxHealth = maxHealth;
        player.Health = maxHealth;

        Room spawnRoom = Room.Get(RoomType.Surface);
        Vector3 offset = Vector3.zero;
        player.Position = new Vector3(124f, 289f, 21f);

        player.ClearInventory();
        Log.Debug("Giving Items to Fifthist");
        player.AddItem(ItemType.GunAK);
        player.AddItem(ItemType.ArmorHeavy);
        CItem.Get<KeycardFifthist>()?.Give(player);
        player.AddItem(ItemType.Medkit);
        player.AddItem(ItemType.Adrenaline);
        player.AddItem(ItemType.SCP500);
        player.AddItem(ItemType.GrenadeHE);
        player.AddAmmo(AmmoType.Nato762,180);
    }
}