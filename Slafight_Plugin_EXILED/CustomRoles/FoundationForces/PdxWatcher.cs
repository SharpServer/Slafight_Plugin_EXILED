using System.Collections.Generic;
using Exiled.API.Enums;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class PdxWatcher : CRole
{
    protected override string RoleName { get; set; } = $"<color={ServerColors.Carmine}><b>Pandra's Box: Watcher</b></color>";
    protected override string Description { get; set; } = "Wardenの補助を行い、アベルを監視する。\n<b>異常を感知したら迅速に上官に連絡する事。</b>";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.PdxWatcher;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "PdxWatcher";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfSergeant;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.GunE11SR,
        ItemType.KeycardMTFCaptain,
        ItemType.Adrenaline,
        ItemType.Adrenaline,
        ItemType.Medkit,
        ItemType.GrenadeFlash,
        ItemType.ArmorHeavy,
        ItemType.Radio,
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato556] = 130,
    };
    protected override string SpawnCustomInfo => $"<color={ServerColors.Carmine}>Pandra's Box Watcher</color>";

}
