using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

namespace Slafight_Plugin_EXILED.CustomRoles.Others;

public class HideAdmin : CRole
{
    protected override string RoleName { get; set; } = "<color=#FF1493><b>THE ADMINISTRATOR</b></color>";
    protected override string Description { get; set; } = "なぁ～んでもできる！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.HideAdmin;
    protected override CTeam Team { get; set; } = CTeam.Others;
    protected override string UniqueRoleKey { get; set; } = "HideAdmin";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Tutorial;
    protected override float? SpawnMaxHealth => 99999f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        typeof(CloakGenerator),
        ItemType.KeycardO5,
    ];
    protected override string SpawnCustomInfo => "<color=#FF1493>THE ADMINISTRATOR</color>";

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        Timing.CallDelayed(0.05f, () =>
        {
            player.EnableEffect<DamageReduction>(255);
            player.EnableEffect<Fade>(255);
            player.EnableEffect<NightVision>(255);
            player.IsBypassModeEnabled = true;
            player.IsNoclipPermitted = true;
            player.IsSpectatable = false;
        });
    }
}
