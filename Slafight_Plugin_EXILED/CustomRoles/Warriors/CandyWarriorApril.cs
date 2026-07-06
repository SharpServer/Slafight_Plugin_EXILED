using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using InventorySystem.Items.Usables.Scp330;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.CustomRoles.Warriors;

public class CandyWarriorApril : CRole
{
    protected override string RoleName { get; set; } = "<color=#FF96DE>CANDY WARRIOR</color>";
    protected override string Description { get; set; } = "非常に<color=#FF96DE>お菓子的</color>である。そうは思わんかね？";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.CandyWarriorApril;
    protected override CTeam Team { get; set; } = CTeam.Warriors;
    protected override string UniqueRoleKey { get; set; } = "CandyWarriorApril";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Tutorial;
    protected override RoleSpawnFlags? SpawnBaseRoleFlags => RoleSpawnFlags.AssignInventory;
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
        TrySetPlayerPosition(player, PositionProvider.GetChaosSpawnPosition(), nameof(CandyWarriorApril));

        const int maxHealth = 1000;
        var playerId = player.Id;

        Timing.CallDelayed(RoleSpawnTimings.AfterRoleSet, () =>
        {
            var current = Player.Get(playerId);
            if (!Check(current) || !IsSafeRolePlayer(current))
                return;

            current.SetCustomInfo("<color=#FF96DE>CANDY WARRIOR</color>");
            current.MaxHealth = maxHealth;
            current.Health = maxHealth;

            current.ClearInventory();
            current.AddItem(ItemType.SCP1509);
            current.AddItem(ItemType.GunCOM18);
            current.AddItem(ItemType.ArmorHeavy);
            current.AddItem(ItemType.SCP500);
            current.AddItem(ItemType.SCP500);
            current.AddItem(ItemType.KeycardO5);
            current.AddItem(ItemType.SCP330);  // 明示的にバッグ追加

            Timing.CallDelayed(RoleSpawnTimings.NextFrame, () =>
            {
                var next = Player.Get(playerId);
                if (!Check(next) || !IsSafeRolePlayer(next))
                    return;

                if (Scp330Bag.TryGetBag(next.ReferenceHub, out var bag))
                {
                    bag.Candies.Clear();
                    for (int i = 0; i < 6; i++)
                        bag.TryAddSpecific(CandyKindID.Pink);
                    bag.ServerRefreshBag();
                }
            });

            current.SetAmmo(AmmoType.Nato9, 50);
            RoleSchematicWears.WearCandyWarrior(current, CRoleTypeId.CandyWarriorApril);
        });
    }
}
