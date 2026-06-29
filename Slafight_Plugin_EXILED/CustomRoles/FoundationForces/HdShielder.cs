using System.Collections.Generic;
using System;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp049;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class HdShielder : CRole
{
    private const float ShieldMaxValue = 100f;
    private const float Scp049ShieldDamage = 50f;

    protected override string RoleName { get; set; } = "<color=#353535>ハンマーダウン シールド兵</color>";
    protected override string Description { get; set; } = "大型シールドで部隊を先導し、シールドが破損するまで敵の攻撃を防ぐ。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.HdShielder;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "HdShielder";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfPrivate;
    protected override float? SpawnMaxHealth => 120f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        typeof(GunFSP18),
        ItemType.KeycardMTFOperative,
        typeof(ArmorInfantry),
        ItemType.Medkit,
        ItemType.Radio,
        ItemType.Flashlight,
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato9] = 120,
    };
    protected override string SpawnCustomInfo => "<color=#727472>Hammer Down Shielder</color>";

    public override void RegisterEvents()
    {
        CustomShieldState.AbsorbingDamage += OnShieldAbsorbingDamage;
        Exiled.Events.Handlers.Scp049.Attacking += OnScp049Attacking;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        CustomShieldState.AbsorbingDamage -= OnShieldAbsorbingDamage;
        Exiled.Events.Handlers.Scp049.Attacking -= OnScp049Attacking;
        base.UnregisterEvents();
    }

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        CustomShieldState.GetOrCreate(player).Configure(
            ShieldMaxValue,
            ShieldMaxValue,
            damageReduction: 0f,
            damageAcceptingThreshold: 0.01f,
            autoDecay: false);
    }

    private void OnShieldAbsorbingDamage(CustomShieldAbsorbingDamageEventArgs ev)
    {
        if (!Check(ev.Player)) return;
        if (!ev.State.CanAcceptDamage) return;

        if (ev.HurtingEvent.IsInstantKill && ev.Attacker != null && ev.Attacker.IsScp)
        {
            ev.HurtingEvent.IsAllowed = false;
            ev.State.ConsumeAll();
            ev.ShieldDamage = 0f;
            ev.HealthDamage = 0f;
            ev.IsAllowed = false;
            return;
        }

        float actualShieldDamage = Math.Min(ev.State.Value, ev.OriginalAmount);

        ev.ShieldDamage = actualShieldDamage;
        ev.HealthDamage = ev.OriginalAmount - actualShieldDamage;
    }

    private void OnScp049Attacking(AttackingEventArgs? ev)
    {
        if (ev?.Target == null || !ev.IsAllowed) return;
        if (!Check(ev.Target)) return;
        if (!CustomShieldState.TryGet(ev.Target, out var state) || !state.CanAcceptDamage) return;

        ev.IsAllowed = false;
        state.Damage(Scp049ShieldDamage);
    }

    protected override void OnRoleDying(DyingEventArgs ev)
    {
        CustomShieldState.Clear(ev.Player);
        base.OnRoleDying(ev);
    }
}
