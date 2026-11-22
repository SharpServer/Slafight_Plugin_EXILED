using System;
using System.Collections.Generic;
using Exiled.API.Features.Pickups;
using InventorySystem;
using InventorySystem.Items;
using InventorySystem.Items.Pickups;
using InventorySystem.Items.Usables;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;
using MEC;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.CustomItems.SCPs;
using Slafight_Plugin_EXILED.Extensions;
using Player = Exiled.API.Features.Player;

namespace Slafight_Plugin_EXILED.CustomItems.Utils;

public class MediHolder : CustomUsableItem
{
    private const int PickupCapacity = 3;
    private static readonly Type[] ExcludedCustomItemTypes =
    [
        typeof(MediHolder),
        typeof(Scp1425)
    ];

    private readonly List<StoredMedicine> _contents = [];
    private Player _ammoOwner;

    private sealed class StoredMedicine(ItemType type, CustomItem custom = null, UsablePickup pickup = null)
    {
        public ItemType Type { get; } = type;
        public CustomItem Custom { get; } = custom;
        public string Name => Custom?.Name ?? Type.ToString();
        public float? UseTime { get; } = pickup?.UseTime;
        public float? MaxCancellableTime { get; } = pickup?.MaxCancellableTime;
        public float? Weight { get; } = pickup?.Weight;
    }

    public override string Name => "MediHolder";
    public override string Description => "拾った回復アイテムを収納する。投げ操作で選び、使用操作で取り出す。";
    public override ItemType BaseType => ItemType.Medkit;
    private CoroutineHandle _hintCoroutine;
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
        _hintCoroutine = scope.Track(Timing.RunCoroutine(DisplayHint(scope, _hintVersion)));
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
                   Check(scope.Player) && ReferenceEquals(Of(Serial), this))
            {
                NormalizeSelection();
                string selected = _contents.Count > 0 ? _contents[_selected].Name : "無し";
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
        Timing.KillCoroutines(_hintCoroutine);
        ClearHint();
    }

    private void ClearHint()
    {
        var player = _hintPlayer;
        bool wasVisible = _hintVisible;
        _hintCoroutine = default;
        _hintPlayer = null;
        _hintVisible = false;

        if (wasVisible && player.IsSafePlayer())
            player.ShowHint(string.Empty);
    }

    internal void AddStartingContents(params ItemType[] types)
    {
        SetAmmoOwner(Owner);
        foreach (ItemType type in types)
            AddMedicine(new StoredMedicine(type));
    }

    protected override void RegisterEvents()
    {
        InventoryExtensions.OnItemRemoved += OnItemRemoved;
        PlayerEvents.DroppingAmmo += OnDroppingAmmo;

        base.RegisterEvents();
    }

    protected override void UnregisterEvents()
    {
        InventoryExtensions.OnItemRemoved -= OnItemRemoved;
        PlayerEvents.DroppingAmmo -= OnDroppingAmmo;

        base.UnregisterEvents();
    }

    private void NormalizeSelection()
    {
        if (_selected >= _contents.Count)
            _selected = 0;
    }

    protected override void OnDropStarting(PlayerDroppingItemEventArgs ev)
    {
        if (!ev.IsAllowed || !ev.Throw) return;
        ev.IsAllowed = false;
        _selected++;
        NormalizeSelection();
    }

    protected override void OnUseStarting(PlayerUsingItemEventArgs ev)
    {
        if (!ev.IsAllowed) return;
        ev.IsAllowed = false;

        Player player = Owner;
        if (!player.IsSafePlayer() || !player.IsAlive) return;
        NormalizeSelection();
        if (_contents.Count == 0)
        {
            player.ShowHint("<size=26>アイテムが何も入っていません！</size>");
            return;
        }

        StoredMedicine medicine = _contents[_selected];
        CustomItem custom = medicine.Custom;
        if (custom != null && !custom.HasStoredPickup)
        {
            RemoveSelectedMedicine();
            player.ShowHint("<size=26>収納していたアイテムは既に失われています。</size>");
            return;
        }

        ushort serial = custom?.Serial ?? 0;
        if (serial != 0 && (custom.Pickup is { IsDestroyed: false } ||
                            InventoryExtensions.ServerTryGetItemWithSerial(serial, out _)))
            return;

        if (player.Inventory.ServerAddItem(medicine.Type, ItemAddReason.PickedUp, serial) is not UsableItem item)
        {
            player.ShowHint("<size=26>インベントリに空きが必要です。</size>");
            return;
        }

        item.UseTime = medicine.UseTime ?? item.UseTime;
        item.MaxCancellableTime = medicine.MaxCancellableTime ?? item.MaxCancellableTime;
        item._weight = medicine.Weight ?? item.Weight;
        custom?.RestoreStoredPickup(LabApi.Features.Wrappers.Item.Get(item));

        RemoveSelectedMedicine();
        player.Inventory.ServerSelectItem(item.ItemSerial);
        PlayerScope.Of(player).Delay(1f, p =>
        {
            if (p.IsAlive && NetGuards.IsReadyClient(p) &&
                ReferenceEquals(p.Inventory.CurInstance, item) && !item.IsUsing)
                UsableItemsController.ServerEmulateMessage(item.ItemSerial, StatusMessage.StatusType.Start);
        });
    }

    protected override void OnPickupCompleted(PlayerPickedUpItemEventArgs ev) =>
        SetAmmoOwner(Player.Get(ev.Player.ReferenceHub));

    protected override void OnDeselected(PlayerChangedItemEventArgs ev) => StopHint();

    protected override void OnTrackingStopped()
    {
        StopHint();
        SetAmmoOwner(null);
        foreach (StoredMedicine medicine in _contents)
        {
            if (medicine.Custom is { HasStoredPickup: true } custom)
                custom.Release();
        }

        _contents.Clear();
        _selected = 0;
        base.OnTrackingStopped();
    }

    protected override void OnOwnerPickupStarting(PlayerPickingUpItemEventArgs ev)
    {
        if (!ev.IsAllowed || _contents.Count >= PickupCapacity ||
            ev.Pickup is not { IsSpawned: true, IsLocked: false } pickup ||
            pickup.Type is not (ItemType.Painkillers or ItemType.Medkit or ItemType.Adrenaline or ItemType.SCP500))
            return;

        CustomItem custom = Of(pickup);
        if (custom != null && Array.Exists(ExcludedCustomItemTypes, type => type.IsInstanceOfType(custom)))
            return;

        var medicine = new StoredMedicine(pickup.Type, custom, Exiled.API.Features.Pickups.Pickup.Get(pickup.Base) as UsablePickup);
        if (custom != null && !custom.TryStorePickup(pickup))
            return;

        ev.IsAllowed = false;
        pickup.IsLocked = true;
        SetAmmoOwner(Owner);
        AddMedicine(medicine);
        // The search completor clears InUse on a denied pickup before destruction.
        Timing.CallDelayed(0f, () =>
        {
            if (!pickup.IsDestroyed && pickup.IsSpawned)
                pickup.Destroy();
        });
    }

    private void OnItemRemoved(ReferenceHub hub, ItemBase item, ItemPickupBase pickup)
    {
        if (!Check(item.ItemSerial)) return;

        StopHint();
        SetAmmoOwner(null);
        if (pickup == null)
            Release();
    }

    private void OnDroppingAmmo(PlayerDroppingAmmoEventArgs ev)
    {
        // Reserve ammo is only the inventory display. Dropping it would spawn
        // real medicine pickups while leaving the stored entries intact.
        if (_ammoOwner != null && ev.Player.ReferenceHub == _ammoOwner.ReferenceHub &&
            _contents.Exists(medicine => medicine.Type == ev.Type))
            ev.IsAllowed = false;
    }

    private void AddMedicine(StoredMedicine medicine)
    {
        _contents.Add(medicine);
        _ammoOwner?.Inventory.ServerAddAmmo(medicine.Type, 1);
    }

    private void RemoveSelectedMedicine()
    {
        _ammoOwner?.Inventory.ServerAddAmmo(_contents[_selected].Type, -1);
        _contents.RemoveAt(_selected);
        NormalizeSelection();
    }

    private void SetAmmoOwner(Player player)
    {
        if (ReferenceEquals(_ammoOwner, player)) return;

        if (_ammoOwner.IsSafePlayer())
        {
            foreach (StoredMedicine medicine in _contents)
                _ammoOwner.Inventory.ServerAddAmmo(medicine.Type, -1);
        }

        _ammoOwner = player.IsSafePlayer() ? player : null;
        if (_ammoOwner == null) return;

        foreach (StoredMedicine medicine in _contents)
            _ammoOwner.Inventory.ServerAddAmmo(medicine.Type, 1);
    }
}
