using System.Collections.Generic;
using Exiled.API.Features.Items;
using LabApi.Events.Arguments.PlayerEvents;
using MEC;
using Slafight_Plugin_EXILED.API.Core.Features;
using Player = Exiled.API.Features.Player;

namespace Slafight_Plugin_EXILED.CustomItems.Utils;

public class MediHolder : CustomUsableItem
{
    public override string Name => "MediHolder";
    public override string Description => "弾薬スロットに拾った回復アイテムを収納でき、使用することができる。";
    public override ItemType BaseType => ItemType.Medkit;
    public CoroutineHandle HintCoroutine;
    public List<ItemType> HolderInventory = [];
    private int _selected;
    
    protected override void OnSelected(PlayerChangedItemEventArgs ev)
    {
        Player player = Owner;
        if (player == null) return;

        PlayerScope.Of(player).Delay(1.25f, _ =>
        {
            HintCoroutine = PlayerScope.Of(player).RunLoop(0.1f, p =>
            {
                if (HolderInventory.Count > 0)
                {
                    p.ShowHint($"<size=26><<color=yellow>{HolderInventory[_selected]}</color>></size>");
                }
                else
                {
                    p.ShowHint($"<size=26><<color=yellow>無し</color>></size>");
                }
            });
        });
    }

    protected override void OnDropStarting(PlayerDroppingItemEventArgs ev)
    {
        if (!ev.Throw) return;
        ev.IsAllowed = false;
        var count = _selected + 1;
        if (HolderInventory.Count <= count)
        {
            _selected = 0;
        }
        else
        {
            _selected++;
        }
    }

    protected override void OnUseStarting(PlayerUsingItemEventArgs ev)
    {
        ev.IsAllowed = false;
        if (HolderInventory.Count <= 0)
        {
            Owner.ShowHint("<size=26>アイテムが何も入っていません！</size>");
            return;
        }

        Owner.CurrentItem = Exiled.API.Features.Items.Item.Create(HolderInventory[_selected]);
        if (Owner.CurrentItem is Usable usable)
        {
            HolderInventory.RemoveAt(_selected);
            _selected = 0;
            PlayerScope.Of(Owner).Delay(1f, _ =>
            {
                usable.MaxCancellableTime = 0f;
                usable.IsUsing = true;
            });
        }
    }

    protected override void OnPickupCompleted(PlayerPickedUpItemEventArgs ev)
    {
        Player player = Owner;
        if (player == null) return;

        foreach (var itemType in HolderInventory)
            AddAmmo(player, itemType);
    }

    protected override void OnDropCompleted(PlayerDroppedItemEventArgs ev)
    {
        Player player = ev.Player?.ReferenceHub is { } hub ? Player.Get(hub) : null;
        if (player == null) return;

        foreach (var itemType in HolderInventory)
            RemoveAmmo(player, itemType);
        
        Timing.KillCoroutines(HintCoroutine);
        player.ShowHint("");

    }

    protected override void OnDeselected(PlayerChangedItemEventArgs ev)
    {
        Timing.KillCoroutines(HintCoroutine);
        if (ev.Player?.ReferenceHub is { } hub)
            Player.Get(hub)?.ShowHint("");
    }

    protected override void OnOwnerPickupStarting(PlayerPickingUpItemEventArgs ev)
    {
        if (ev.Pickup == null) return;
        if (ev.Pickup.Serial == Serial) return;

        if (HolderInventory.Count >= 3)
            return;

        if (Is(ev.Pickup, out _))
            return;

        if (ev.Pickup.Type is not (
            ItemType.Painkillers or
            ItemType.Medkit or
            ItemType.Adrenaline or
            ItemType.SCP500))
            return;

        ev.IsAllowed = false;

        HolderInventory.Add(ev.Pickup.Type);
        if (Owner is { } owner)
            AddAmmo(owner, ev.Pickup.Type);

        ev.Pickup.Destroy();
    }
    
    private static void AddAmmo(Player player, ItemType type)
    {
        if (player.Ammo.ContainsKey(type))
            player.Ammo[type]++;
        else
            player.Ammo.Add(type, 1);
    }

    private static void RemoveAmmo(Player player, ItemType type)
    {
        if (!player.Ammo.TryGetValue(type, out var amount) || amount == 0)
            return;

        player.Ammo[type]--;
    }
}
