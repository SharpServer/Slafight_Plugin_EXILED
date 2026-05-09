using Exiled.API.Enums;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.CustomRoles.Others;

public class HideAdmin : CRole
{
    protected override string RoleName { get; set; } = "<color=#FF1493><b>THE ADMINISTRATOR</b></color>";
    protected override string Description { get; set; } = "なぁ～んでもできる！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.HideAdmin;
    protected override CTeam Team { get; set; } = CTeam.Others;
    protected override string UniqueRoleKey { get; set; } = "HideAdmin";

    public override void SpawnRole(Player? player, RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.Tutorial);
        player.UniqueRole = UniqueRoleKey;

        const int maxHealth = 99999;

        Timing.CallDelayed(0.05f, () =>
        {
            player.SetCustomInfo("<color=#FF1493>THE ADMINISTRATOR</color>");
            player.MaxHealth = maxHealth;
            player.Health = maxHealth;
            player.EnableEffect(EffectType.DamageReduction, 255);
            player.IsBypassModeEnabled = true;
            player.IsNoclipPermitted = true;

            CItem.Get<CloakGenerator>()?.Give(player);
            player.AddItem(ItemType.KeycardO5);
            player.IsSpectatable = false;
        });
    }
}