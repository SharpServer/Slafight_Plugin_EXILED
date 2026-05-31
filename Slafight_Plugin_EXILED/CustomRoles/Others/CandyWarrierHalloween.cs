using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using InventorySystem.Items.Usables.Scp330;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.MainHandlers;

namespace Slafight_Plugin_EXILED.CustomRoles.Others;

public class CandyWarrierHalloween : CRole
{
    protected override string RoleName { get; set; } = "<color=#EE7600>CANDY WARRIER</color>";
    protected override string Description { get; set; } = "非常に<color=#EE7600>お菓子的</color>である。そうは思わんかね？";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.CandyWarrierHalloween;
    protected override CTeam Team { get; set; } = CTeam.Others;
    protected override string UniqueRoleKey { get; set; } = "CandyWarrierHalloween";
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

        const int maxHealth = 1000;

        Timing.CallDelayed(RoleSpawnTimings.AfterRoleSet, () =>
        {
            player.SetCustomInfo("<color=#EE7600>CANDY WARRIER</color>");
            player.MaxHealth = maxHealth;
            player.Health = maxHealth;

            player.ClearInventory();
            player.AddItem(ItemType.SCP1509);
            player.AddItem(ItemType.GunCOM18);
            player.AddItem(ItemType.ArmorHeavy);
            player.AddItem(ItemType.SCP500);
            player.AddItem(ItemType.SCP500);
            player.AddItem(ItemType.KeycardO5);
            player.AddItem(ItemType.SCP330);  // 明示的にバッグ追加

            Timing.CallDelayed(RoleSpawnTimings.NextFrame, () =>
            {
                if (Scp330Bag.TryGetBag(player.ReferenceHub, out var bag))
                {
                    bag.Candies.Clear();
                    var rareCandies = new List<CandyKindID>
                    {
                        CandyKindID.Black,
                        CandyKindID.Brown,
                        CandyKindID.Gray,
                        CandyKindID.Orange,
                        CandyKindID.White,
                    };
                    for (int i = 0; i < 6; i++)
                        bag.TryAddSpecific(rareCandies.RandomItem());
                    bag.ServerRefreshBag();
                }
            });

            player.SetAmmo(AmmoType.Nato9, 50);
            LabApiHandler.SchemCandyWarrier(LabApi.Features.Wrappers.Player.Get(player.ReferenceHub));
        });
    }
}
