using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp173;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class Sculpture : CRole
{
    protected override string RoleName { get; set; } = "Sculpture";
    protected override string Description { get; set; } = "相手が瞬きしたときに高速で移動し、痛めつける。\n財団の味方である。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Sculpture;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "Sculpture";

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Scp173.Blinking += OnBlinking;
        Exiled.Events.Handlers.Scp173.AddingObserver += OnObserving;
        Exiled.Events.Handlers.Player.Hurting += OnNecking;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Scp173.Blinking -= OnBlinking;
        Exiled.Events.Handlers.Scp173.AddingObserver -= OnObserving;
        Exiled.Events.Handlers.Player.Hurting -= OnNecking;
        base.UnregisterEvents();
    }
    
    public override void SpawnRole(Player? player,RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.NtfPrivate);
        player.Role.Set(RoleTypeId.Scp173, RoleSpawnFlags.AssignInventory);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 500f;
        player.Health = player.MaxHealth;
        player.MaxHumeShield = 300f;
        player.HumeShield = player.MaxHumeShield;
        player.SetScale(new Vector3(0.8f, 1f, 0.8f));
        player.ClearInventory();
        player.SetCustomInfo("<color=#00B7EB>Sculpture</color>");

        player.EnableEffect(EffectType.Slowness, 20);
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

    private void OnNecking(HurtingEventArgs ev)
    {
        if (!Check(ev.Attacker)) return;
        if (ev.DamageHandler.Type == DamageType.Scp173 && ev.IsInstantKill)
        {
            ev.IsAllowed = false;
            ev.Player.Hurt(ev.Attacker, 35f, DamageType.Scp173);
            ev.Attacker.ShowHitMarker();
        }
    }

    protected override void OnDying(DyingEventArgs ev)
    {
        Exiled.API.Features.Cassie.Clear();
        base.OnDying(ev);
    }
}