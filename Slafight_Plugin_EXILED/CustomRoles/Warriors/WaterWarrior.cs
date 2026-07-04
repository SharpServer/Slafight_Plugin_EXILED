using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.Abilities;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.Warriors;

public class WaterWarrior : CRole
{
    protected override string RoleName { get; set; } = $"<color={ServerColors.Aqua}>WATER WARRIOR</color>";
    protected override string Description { get; set; } = $"<size=24><color={ServerColors.Aqua}>夏にヒャッハーしてる謎の勢力。</color>\n" +
                                                          $"水鉄砲を使って施設を<color=red>SUMMER AURA</color>で制圧しろ！</size>";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.WaterWarrior;
    protected override CTeam Team { get; set; } = CTeam.Warriors;
    protected override string UniqueRoleKey { get; set; } = "WaterWarrior";
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
        RoleSchematicWears.WearWarrior(player, CRoleTypeId.WaterWarrior, "WaterWarriorsModel", Color.black);

        const int maxHealth = 1000;
        var playerId = player.Id;

        Timing.CallDelayed(RoleSpawnTimings.AfterRoleSet, () =>
        {
            var current = Player.Get(playerId);
            if (!Check(current) || !IsSafeRolePlayer(current))
                return;

            CustomInfoDisplay.Apply(current, $"<color={ServerColors.Aqua}>WATER WARRIOR</color>");
            current.MaxHealth = maxHealth;
            current.Health = maxHealth;

            current.AddItem(ItemType.SCP1509);
            current.AddItem(ItemType.ArmorHeavy);
            current.AddItem(ItemType.SCP500);
            current.AddItem(ItemType.SCP500);
            current.AddItem(ItemType.KeycardO5);

            GiveCItem<AquaBlaster>(current, true);
            GiveCItem<HydroCannon>(current, true);

            current.AddAbility<AquaJumpAbility>();
            current.AddAbility<AquaSplashAbility>();

            current.SetAmmo(AmmoType.Nato9, 50);
        });
    }
}
