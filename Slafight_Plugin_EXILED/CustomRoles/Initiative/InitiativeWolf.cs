using System.Collections.Generic;
using AdminToys;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Roles;
using Exiled.Events.EventArgs.Scp049;
using PlayerRoles;
using ProjectMER.Features.Extensions;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.MainHandlers;
using Slafight_Plugin_EXILED.Patches;
using UnityEngine;
using Light = Exiled.API.Features.Toys.Light;

namespace Slafight_Plugin_EXILED.CustomRoles.Initiative;

public class InitiativeWolf : CRole
{
    protected override string RoleName { get; set; } = "Horizon Initiative Wolf";
    protected override string Description { get; set; } = "Xx_ULTIMATE_xX";
    protected override string? SpawnCustomInfo => $"<color={ServerColors.BlueGreen}>Horizon Initiative Wolf</color>";
    protected override string UniqueRoleKey { get; set; } = "InitiativeWolf";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.InitiativeWolf;
    protected override CTeam Team { get; set; } = CTeam.Initiative;
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfPrivate;
    public override bool CanUseProximityChat => true;
    protected override IReadOnlyList<CRoleEffect> SpawnEffects => 
    [
        new(EffectType.MovementBoost, 23)
    ];

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Scp049.ActivatingSense += OnActivatingSense;
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Scp049.ActivatingSense -= OnActivatingSense;
    }

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        player.Role.Set(RoleTypeId.Scp049, RoleSpawnFlags.AssignInventory);
        AssignIdentity(player);

        player.MaxHealth = 110f;
        player.Health = player.MaxHealth;
        player.MaxHumeShield = 0f;
        player.HumeShield = player.MaxHumeShield;
        
        player.TryWear("SCP035", player.Transform, out var schematicObject, (Vector3.forward * 0.05f)+(Vector3.up*0.65f), slot: "head");
        schematicObject.Scale *= 1.285f;
        LabApi.Features.Wrappers.Player.Get(player.NetId)!.DestroySchematic(schematicObject);
        var light = Light.Create(player.Position);
        light.LightType = LightType.Point;
        light.ShadowType = LightShadows.None;
        light.Color = Color.yellow;
        light.Range = 20f;
        light.Intensity = 8f;
        player.TryWear(light.AdminToyBase, out var toy, slot: "bodyLight");
        toy.transform.localPosition = Vector3.zero;
        base.OnRoleSpawned(player, roleSpawnFlags);
    }

    private void OnActivatingSense(ActivatingSenseEventArgs ev)
    {
        if (ev == null || !Check(ev.Player))
            return;

        ev.Target = Scp049InitiativeSensePatch.TryFindInitiativeTarget(ev.Scp049.SenseAbility, out Player target)
            ? target
            : null;
    }
}
