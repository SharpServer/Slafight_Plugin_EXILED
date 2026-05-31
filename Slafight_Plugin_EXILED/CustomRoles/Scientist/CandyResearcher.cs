using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using InventorySystem.Items.Usables.Scp330;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.Scientist;

public class CandyResearcher : CRole
{
    protected override string RoleName { get; set; } = "お菓子研究者";
    protected override string Description { get; set; } = "兎に角甘いものが好きな科学者。\nキャンディー大好き！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.CandyResearcher;
    protected override CTeam Team { get; set; } = CTeam.Scientists;
    protected override string UniqueRoleKey { get; set; } = "CandyResearcher";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Scientist;
    protected override float? SpawnMaxHealth => 100f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.KeycardScientist,
        ItemType.SCP330,
    ];
    protected override string SpawnCustomInfo => "Candy Researcher";

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
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
        
        Door.Get(DoorType.Scp330).IsOpen = true;
        player.Position = Door.Get(DoorType.Scp330).Position + (Vector3.up * 0.8f);
    }
}
