using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.MainHandlers;

namespace Slafight_Plugin_EXILED.CustomRoles.Moderators;

public class ModeratorRole : CRole
{
    protected override string RoleName { get; set; } = $"<color={ServerColors.Pink}><b>Law's Left Hand: Moderator</b></color>";
    protected override string Description { get; set; } = "Omega-1内に存在する極秘治安維持隊\n" +
                                                          "正しくモデレーション処置を行い、\n" +
                                                          "秩序を回復し、安全なシャープ鯖を死守せよ！";
    protected override string SpawnCustomInfo => $"<color={ServerColors.Pink}>Law's Left Hand Moderator</color>";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.ModeratorRole;
    protected override CTeam Team { get; set; } = CTeam.Moderators;
    protected override string UniqueRoleKey { get; set; } = "Moderator";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Tutorial;
    protected override float? SpawnMaxHealth => 999f;
    protected override IReadOnlyList<SpecificFlagType> SpawnSpecificFlags => 
    [
        SpecificFlagType.RPNameDisabled    
    ];
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.KeycardMTFCaptain,
        typeof(ModeratorUtil),
        ItemType.Radio,
        typeof(ArmorVip),
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato9] = 80,
    };

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        RPNameSetter.SetForcedCustomName(player, $"{player.Nickname} - MODERATION MODE");
    }
}
