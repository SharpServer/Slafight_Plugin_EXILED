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

public class SneOperator : CRole
{
    protected override string RoleName { get; set; } = "<color=#FF1493>シー・ノー・イービル オペレーター</color>";
    protected override string Description { get; set; } = "部隊を指揮し、気狂いどもに正常性による一撃を与えよ！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.SneOperator;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "SneOperator";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfCaptain;
    protected override float? SpawnMaxHealth => 150f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.GunFRMG0,
        ItemType.KeycardMTFCaptain,
        typeof(SerumC),
        typeof(AntiMemeGoggle),
        typeof(NeutralizeGrenade),
        typeof(NeutralizeGrenade),
        ItemType.Radio,
        ItemType.ArmorHeavy,
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato556] = 200,
    };
    protected override string SpawnCustomInfo => "<color=#FF1493>See No Evil Operator</color>";
    protected override void OnRoleHurting(HurtingEventArgs ev)
    {
        if (ev.Attacker.GetTeam() is CTeam.Fifthists || ev.Attacker.GetCustomRole() is CRoleTypeId.Scp3005)
        {
            ev.Amount *= 0.77f; 
        }
        base.OnRoleHurting(ev);
    }
}
