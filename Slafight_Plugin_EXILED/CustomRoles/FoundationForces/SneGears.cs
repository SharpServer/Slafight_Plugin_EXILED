using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.Events.EventArgs.Player;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class SneGears : CRole
{
    protected override string RoleName { get; set; } = "<color=#FF1493>シー・ノー・イービル 対圧兵</color>";
    protected override string Description { get; set; } = "気狂いどもに一撃を与えろ！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.SneGears;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "SneGears";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfSergeant;
    protected override float? SpawnMaxHealth => 125f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.GunE11SR,
        ItemType.KeycardMTFOperative,
        typeof(SerumC),
        ItemType.Medkit,
        typeof(AntiMemeGoggle),
        ItemType.Radio,
        ItemType.ArmorHeavy,
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato556] = 140,
    };
    protected override string SpawnCustomInfo => "<color=#FF1493>See No Evil Gears</color>";
    protected override void OnRoleHurting(HurtingEventArgs ev)
    {
        if (ev.Attacker.GetTeam() is CTeam.Fifthists || ev.Attacker.GetCustomRole() is CRoleTypeId.Scp3005)
        {
            ev.Amount *= 0.77f; 
        }
        base.OnRoleHurting(ev);
    }
}
