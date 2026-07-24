using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp173;
using Exiled.Events.Handlers;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Player = Exiled.API.Features.Player;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class Sculpture : CRole
{
    protected override string RoleName { get; set; } = "Sculpture";
    protected override string Description { get; set; } = "相手が瞬きしたときに高速で移動し、痛めつける。\n財団の味方である。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Sculpture;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "Sculpture";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Scp173;
    protected override RoleSpawnFlags? SpawnBaseRoleFlags => RoleSpawnFlags.AssignInventory;
    protected override IReadOnlyList<CRoleEffect> SpawnEffects =>
    [
        new(EffectType.Slowness, 20)
    ];

    public override void RegisterEvents()
    {
        Scp173.Blinking += OnBlinking;
        Scp173.AddingObserver += OnObserving;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Scp173.Blinking -= OnBlinking;
        Scp173.AddingObserver -= OnObserving;
        base.UnregisterEvents();
    }
    
    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        TrySetPlayerPosition(player, PositionProvider.GetNtfSpawnPosition(), nameof(Sculpture));
        player.MaxHealth = 500f;
        player.Health = player.MaxHealth;
        player.MaxHumeShield = 300f;
        player.HumeShield = player.MaxHumeShield;
        player.SetScale(new Vector3(0.8f, 1f, 0.8f));
        player.ClearInventory();
        player.SetCustomInfo("<color=#00B7EB>Sculpture</color>");
    }

    private void OnBlinking(BlinkingEventArgs ev)
    {
        if (!Check(ev.Player)) return;
        if (ev.Targets.Count >= 3)
        {
            ev.Scp173.BlinkReady = false;
        }
    }

    private void OnObserving(AddingObserverEventArgs ev)
    {
        if (!Check(ev.Player)) return;
        if (ev.Observer.GetTeam() == CTeam.FoundationForces || ev.Observer.GetTeam() == CTeam.Guards)
        {
            ev.IsAllowed = false;
        }
    }

    protected override void OnRoleHurtingOthers(HurtingEventArgs ev)
    {
        if (ev.DamageHandler.Type == DamageType.Scp173 && ev.IsInstantKill)
        {
            ev.IsAllowed = false;
            ev.Player.Hurt(ev.Attacker, 35f, DamageType.Scp173);
            ev.Attacker.ShowHitMarker();
        }
    }

    protected override void OnRoleDying(DyingEventArgs ev)
    {
        Exiled.API.Features.Cassie.Clear();
        base.OnRoleDying(ev);
    }
}
