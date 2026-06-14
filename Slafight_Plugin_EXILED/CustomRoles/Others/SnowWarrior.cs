using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.MainHandlers;

namespace Slafight_Plugin_EXILED.CustomRoles.Others;

public class SnowWarrior : CRole
{
    protected override string RoleName { get; set; } = "<color=white>SNOW WARRIOR</color>";
    protected override string Description { get; set; } = "<size=24>非常に<color=#ffffff>雪玉的</color>である。そうは思わんかね？";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.SnowWarrior;
    protected override CTeam Team { get; set; } = CTeam.Others;
    protected override string UniqueRoleKey { get; set; } = "SnowWarrior";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.ChaosRifleman;
    protected override IReadOnlyList<CRoleEffect> SpawnEffects =>
    [
        new(EffectType.Slowness, 10)
    ];

    protected override IReadOnlyList<SpecificFlagType> SpawnSpecificFlags =>
    [
        SpecificFlagType.SpecialWeaponsDisabled
    ];

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        player.Role.Set(RoleTypeId.Tutorial, RoleSpawnFlags.AssignInventory);
        LabApiHandler.SchemSnowWarrior(LabApi.Features.Wrappers.Player.Get(player.ReferenceHub));

        const int maxHealth = 1000;
        var playerId = player.Id;

        Timing.CallDelayed(RoleSpawnTimings.AfterRoleSet, () =>
        {
            var current = Player.Get(playerId);
            if (!Check(current) || !IsSafeRolePlayer(current))
                return;

            CustomInfoDisplay.Apply(current, "<color=#FFFFFF>SNOW WARRIOR</color>");
            current.MaxHealth = maxHealth;
            current.Health = maxHealth;

            current.AddItem(ItemType.SCP1509);
            current.AddItem(ItemType.GunCOM18);
            current.AddItem(ItemType.ArmorHeavy);
            current.AddItem(ItemType.SCP500);
            current.AddItem(ItemType.SCP500);
            current.AddItem(ItemType.KeycardO5);

            current.SetAmmo(AmmoType.Nato9, 50);
        });
    }
}
