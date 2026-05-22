using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class SneNeutralitist : CRole
{
    protected override string RoleName { get; set; } = "<color=#FF1493>シー・ノー・イービル 破力兵</color>";
    protected override string Description { get; set; } = "気狂いどもを食い止めろ！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.SneNeutralitist;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "SneNeutralitist";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfPrivate;
    protected override float? SpawnMaxHealth => 125f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.GunE11SR,
        ItemType.KeycardMTFOperative,
        ItemType.Adrenaline,
        ItemType.Medkit,
        typeof(NeutralizeGrenade),
        typeof(NeutralizeGrenade),
        ItemType.Radio,
        ItemType.ArmorCombat,
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato556] = 120,
    };
    protected override string SpawnCustomInfo => "<color=#FF1493>See No Evil Neutralitist</color>";
    protected override void OnRoleHurting(HurtingEventArgs ev)
    {
        if (ev.Attacker.GetTeam() is CTeam.Fifthists || ev.Attacker.GetCustomRole() is CRoleTypeId.Scp3005)
        {
            ev.Amount *= 0.77f; 
        }
        base.OnRoleHurting(ev);
    }
}
