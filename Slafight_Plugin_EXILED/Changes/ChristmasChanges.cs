using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using Exiled.CustomItems.API.Features;
using Exiled.Events.EventArgs.Scp2536;
using InventorySystem.Items.Usables.Scp330;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Interface;
using EffectType = Exiled.API.Enums.EffectType;
using Random = UnityEngine.Random;

namespace Slafight_Plugin_EXILED.Changes;

public class ChristmasChanges : IBootstrapHandler, IDisposable
{
    public static ChristmasChanges Instance { get; private set; }
    public static void Register()
    {
        Unregister();
        Instance = new();
    }

    public static void Unregister()
    {
        Instance?.Dispose();
        Instance = null;
    }

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        //Exiled.Events.Handlers.Scp2536.GrantingGift -= TreeChange;
        GC.SuppressFinalize(this);
    }

    public void TreeChange(GrantingGiftEventArgs ev)
    {
        if (ev.Player.Role.Team != Team.SCPs)
        {
            var Lucky = Random.Range(0f,1f);
            var player = ev.Player;
            ev.IsAllowed = false;
            if (Lucky <= 0f)
            {
                player.ClearInventory();
                player.AddItem(ItemType.GrenadeHE);
                player.AddItem(ItemType.GrenadeHE);
                player.AddItem(ItemType.GrenadeHE);
                player.AddItem(ItemType.SCP018);
                player.AddItem(ItemType.SCP018);
                player.AddItem(ItemType.SCP018);
                player.TryAddCandy(CandyKindID.Pink);
                player.TryAddCandy(CandyKindID.Pink);
                player.TryAddCandy(CandyKindID.Pink);
                player.TryAddCandy(CandyKindID.Pink);
                player.TryAddCandy(CandyKindID.Pink);
                player.TryAddCandy(CandyKindID.Pink);
                player.TryAddCandy(CandyKindID.Pink);
                player.TryAddCandy(CandyKindID.Pink);
                CustomItem.TryGive(player, 700,false);
                player.ShowHint("とてもレアなプレゼントの様だ・・・インベントリを見てみよう！=)");
            }
            else if (Lucky <= 0.25f)
            {
                foreach (Item item in player.Items.ToList())
                {
                    player.EnableEffect(EffectType.Scp207);
                    break;
                }
            }
            else if (Lucky <= 0.3f)
            {
                if (!player.IsInventoryFull)
                {
                    player.AddItem(ItemType.Scp021J);
                }
                else
                {
                    Pickup.CreateAndSpawn(ItemType.Scp021J, ev.Player.Position);
                }
            }
            else if (Lucky <= 0.4f)
            {
                if (!player.IsInventoryFull || player.HasItem(ItemType.SCP330))
                {
                    float random = Random.Range(0f, 1f);
                    List<CandyKindID> rareCandies =
                    [
                        CandyKindID.Black,
                        CandyKindID.Brown,
                        CandyKindID.Gray,
                        CandyKindID.Orange,
                        CandyKindID.White,
                        CandyKindID.Evil
                    ];
        
                    List<CandyKindID> normalCandies =
                    [
                        CandyKindID.Red,
                        CandyKindID.Blue,
                        CandyKindID.Green,
                        CandyKindID.Purple,
                        CandyKindID.Rainbow,
                        CandyKindID.Yellow
                    ];

                    if (random <= 0.1f)
                    {
                        player.TryAddCandy(CandyKindID.Pink);
                    }
                    else if (random <= 0.22f)
                    {
                        player.TryAddCandy(rareCandies.RandomItem());
                    }
                    else
                    {
                        player.TryAddCandy(normalCandies.RandomItem());
                    }
                }
                else
                {
                    player.ExplodeEffect(ProjectileType.FragGrenade);
                    player.ShowHint("Boooooooo!!!");
                }
            }
            else
            {
                if (!player.IsInventoryFull)
                {
                    player.AddItem(Item.List?.GetRandomValue());
                }
                else
                {
                    Pickup.CreateAndSpawn(ItemType.Coal, ev.Player.Position);
                }
            }
        }
    }
}
