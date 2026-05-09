using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Pickups.Projectiles;
using Exiled.Events.EventArgs.Player;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.ChaosInsurgency;

public class ChaosCommando : CRole
{
    protected override string RoleName { get; set; } = "カオス・インサージェンシー コマンドー";
    protected override string Description { get; set; } = "カオスの実戦部隊の中でのエリート中のエリート。\n抑圧兵よりも階級は上で、基本的に秩序のない、襲撃部隊を指揮する。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.ChaosCommando;
    protected override CTeam Team { get; set; }  = CTeam.ChaosInsurgency;
    protected override string UniqueRoleKey { get; set; } = "CI_Commando";

    public override void SpawnRole(Player? player, RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.ChaosRepressor);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 120;
        player.Health = player.MaxHealth;
        
        player.ClearInventory();
        Log.Debug("Giving Items to ChaosCommando");
        player.AddItem(ItemType.KeycardChaosInsurgency);
        player.AddItem(ItemType.Adrenaline);
        player.AddItem(ItemType.Medkit);
        CItem.Get<AdvancedMedkit>()?.Give(player);
        CItem.Get<ArmorInfantry>()?.Give(player);
        CItem.Get<GunSuperLogicer>()?.Give(player);
        
        player.AddAmmo(AmmoType.Nato762, 300);
            
        player.SetCustomInfo("Chaos Insurgency Commando");
    }

    protected override void OnDying(DyingEventArgs ev)
    {
        Projectile.CreateAndSpawn(ProjectileType.FragGrenade,ev.Player.Position + Vector3.up * 0.5f);
        base.OnDying(ev);
    }
}