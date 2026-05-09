using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using InventorySystem.Items.Usables.Scp330;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.ClassD;

public class CandySubject : CRole
{
    protected override string RoleName { get; set; } = "<color=#ee7600>菓子被験者</color>";
    protected override string Description { get; set; } = "お菓子が大好きな変な博士の実験に巻き込まれた可愛そうなDクラス職員。\n" +
                                                          "いっぱいキャンディーを持たされている。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.CandySubject;
    protected override CTeam Team { get; set; } = CTeam.ClassD;
    protected override string UniqueRoleKey { get; set; } = "CandySubject";

    public override void SpawnRole(Player? player,RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.ClassD);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 100;
        player.Health = player.MaxHealth;
        player.ClearInventory();
        player.AddItem(ItemType.KeycardJanitor);
        player.AddItem(ItemType.SCP330);  // 明示的にバッグ追加

        Timing.CallDelayed(0.02f, () =>
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
        
        Door.Get(DoorType.Scp330Chamber).IsOpen = true;
        player.Position = Door.Get(DoorType.Scp330Chamber).Position + (Vector3.up * 0.8f);
            
        player.SetCustomInfo("Candy Subject");
    }
}