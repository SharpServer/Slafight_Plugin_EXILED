using System.Collections.Generic;
using Exiled.API.Features.Items;
using LabApi.Events.Arguments.PlayerEvents;
using MEC;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.Extensions;
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
    private int _hintVersion;
    private Player _hintPlayer;
    private bool _hintVisible;
    
    protected override void OnSelected(PlayerChangedItemEventArgs ev)
    {
        StopHint();
        Player player = Owner;
        if (!player.IsSafePlayer()) return;

        var scope = PlayerScope.Of(player);
        _hintPlayer = player;
        HintCoroutine = scope.Track(Timing.RunCoroutine(DisplayHint(scope, _hintVersion)));
    }

    private IEnumerator<float> DisplayHint(PlayerScope scope, int version)
    {
        try
        {
            // Keep the initial delay and display loop under the same cancellable handle.
            yield return Timing.WaitForSeconds(1.25f);
            while (version == _hintVersion && !scope.IsDisposed &&
                   scope.Player.IsSafePlayer() && scope.Player.IsAlive &&
                   scope.Player.GetNetId() == scope.NetId &&
                   ReferenceEquals(Owner, scope.Player) &&
                   scope.Player.CurrentItem?.Serial == Serial && ReferenceEquals(Of(Serial), this))
            {
                NormalizeSelection();
                string selected = HolderInventory.Count > 0 ? HolderInventory[_selected].ToString() : "無し";
                _hintVisible = true;
                // Let the last frame expire even if a lifecycle event bypasses normal cleanup.
                scope.Player.ShowHint($"<size=26><<color=yellow>{selected}</color>></size>", 0.3f);
                yield return Timing.WaitForSeconds(0.1f);
            }
        }
        finally
        {
            // A cancelled, older session must never clear a newly selected holder's display.
            if (version == _hintVersion)
                ClearHint();
        }
    }

    private void StopHint()
    {
        _hintVersion++;
        Timing.KillCoroutines(HintCoroutine);
        ClearHint();
    }

    private void ClearHint()
    {
        var player = _hintPlayer;
        bool wasVisible = _hintVisible;
        HintCoroutine = default;
        _hintPlayer = null;
        _hintVisible = false;

        if (wasVisible && player.IsSafePlayer())
            player.ShowHint(string.Empty);
    }

    private void NormalizeSelection()
    {
        if (_selected < 0 || _selected >= HolderInventory.Count)
            _selected = 0;
    }

    protected override void OnDropStarting(PlayerDroppingItemEventArgs ev)
    {
        if (!ev.Throw) return;
        ev.IsAllowed = false;
        NormalizeSelection();
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
        Player player = Owner;
        if (!player.IsSafePlayer()) return;
        NormalizeSelection();
        if (HolderInventory.Count <= 0)
        {
            player.ShowHint("<size=26>アイテムが何も入っていません！</size>");
            return;
        }

        player.CurrentItem = Exiled.API.Features.Items.Item.Create(HolderInventory[_selected]);
        if (player.CurrentItem is Usable usable)
        {
            HolderInventory.RemoveAt(_selected);
            _selected = 0;
            PlayerScope.Of(player).Delay(1f, p =>
            {
                if (!p.IsAlive || !ReferenceEquals(p.CurrentItem?.Base, usable.Base))
                    return;

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
        StopHint();
        Player player = ev.Player?.ReferenceHub is { } hub ? Player.Get(hub) : null;
        if (player == null) return;

        foreach (var itemType in HolderInventory)
            RemoveAmmo(player, itemType);
    }

    protected override void OnDeselected(PlayerChangedItemEventArgs ev) => StopHint();

    protected override void OnTrackingStopped()
    {
        StopHint();
        base.OnTrackingStopped();
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
