using System.Collections.Generic;
using System.Collections.ObjectModel;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
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
    protected override string? SpawnCustomInfo => $"<color={ServerColors.Aqua}>WATER WARRIOR</color>";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Tutorial;
    protected override IReadOnlyList<CRoleEffect> SpawnEffects =>
    [
        new(EffectType.Slowness, 10)
    ];

    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>()
    {
        [AmmoType.Nato9] = 220,
        [AmmoType.Nato556] = 220
    };

    protected override IReadOnlyList<SpecificFlagType> SpawnSpecificFlags =>
    [
        SpecificFlagType.SpecialWeaponsDisabled
    ];

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        player.Position = PositionProvider.GetChaosSpawnPosition();
        RoleSchematicWears.WearWarrior(player, CRoleTypeId.WaterWarrior, "WaterWarriorsModel", Color.black);

        const int maxHealth = 1000;

        player.MaxHealth = maxHealth;
        player.Health = maxHealth;

        player.AddItem(ItemType.SCP1509);
        player.AddItem(ItemType.ArmorHeavy);
        player.AddItem(ItemType.SCP500);
        player.AddItem(ItemType.SCP500);
        player.AddItem(ItemType.KeycardO5);

        GiveCItem<AquaBlaster>(player, true);
        GiveCItem<HydroCannon>(player, true);

        player.AddAbility<AquaJumpAbility>();
        player.AddAbility<AquaSplashAbility>();

        player.SetAmmo(AmmoType.Nato9, 50);
    }

    protected override void OnRoleHurting(HurtingEventArgs ev)
    {
        if (ev.DamageHandler.Type is DamageType.Tesla)
        {
            ev.Amount *= 1.25f;
        }
        base.OnRoleHurting(ev);
    }
}
