using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features.Pickups.Projectiles;
using Exiled.Events.EventArgs.Player;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.ChaosInsurgency;

public class ChaosCommando : CRole
{
    protected override string RoleName { get; set; } = "カオス・インサージェンシー コマンドー";
    protected override string Description { get; set; } = "カオスの実戦部隊の中でのエリート中のエリート。\n抑圧兵よりも階級は上で、基本的に秩序のない、襲撃部隊を指揮する。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.ChaosCommando;
    protected override CTeam Team { get; set; }  = CTeam.ChaosInsurgency;
    protected override string UniqueRoleKey { get; set; } = "CI_Commando";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.ChaosRepressor;
    protected override float? SpawnMaxHealth => 120f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.KeycardChaosInsurgency,
        ItemType.Adrenaline,
        ItemType.Medkit,
        typeof(AdvancedMedkit),
        typeof(ArmorInfantry),
        typeof(GunSuperLogicer),
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato762] = 130,
    };
    protected override string SpawnCustomInfo => "Chaos Insurgency Commando";

    protected override void OnRoleDying(DyingEventArgs ev)
    {
        Projectile.CreateAndSpawn(ProjectileType.FragGrenade,ev.Player.Position + Vector3.up * 0.5f);
        base.OnRoleDying(ev);
    }
}
