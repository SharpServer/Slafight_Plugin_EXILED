using System;
using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Item;
using Exiled.Events.EventArgs.Player;
using MEC;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps.Core;
using SNAPI.Events.EventArgs;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems.IntermediateBases;

public abstract class AccessTunerBase : CItem
{
    protected override ItemType BaseItem => ItemType.KeycardChaosInsurgency;
    public virtual AccessTunerLevel AccessLevel { get; protected set; } = AccessTunerLevel.Undefined;
    protected override string? PickupSchematicName => GetModelName(AccessLevel);
    public readonly Dictionary<ushort, AccessTunerService> Services = [];
    
    public class AccessTunerService(AccessTunerLevel accessTunerLevel = AccessTunerLevel.Undefined, byte tunePoints = 0, byte usedCounts = 0)
    {
        public readonly AccessTunerLevel AccessTunerLevel = accessTunerLevel;
        public int TunePoints = tunePoints;
        public int UsedCounts = usedCounts;
        public string LastHackResult = "未実行";
    }
    public enum AccessTunerLevel
    {
        LevelOne = 1,
        LevelTwo = 2,
        LevelThree = 3,
        Broken = -1,
        Undefined = -2
    }

    protected override void OnWaitingForPlayers()
    {
        Services.Clear();
        base.OnWaitingForPlayers();
    }

    protected override void CustomizeItem(Item item)
    {
        GetOrCreateService(item.Serial);
        ApplyNoPermissions(item);
        base.CustomizeItem(item);
    }

    protected override void CustomizePickup(Pickup pickup)
    {
        GetOrCreateService(pickup.Serial);
        ApplyNoPermissions(pickup);
        base.CustomizePickup(pickup);
    }

    protected override string? ResolvePickupSchematicName(Pickup pickup)
    {
        AccessTunerService service = GetOrCreateService(pickup.Serial);
        AccessTunerLevel level = NormalizeAccessLevel(service.AccessTunerLevel);
        return GetModelName(level);
    }

    protected override void OnAcquired(ItemAddedEventArgs ev, bool displayMessage)
    {
        GetOrCreateService(ev.Item.Serial);
        ApplyNoPermissions(ev.Item);
        ReapplyNoPermissionsDelayed(ev.Item);
        base.OnAcquired(ev, displayMessage);
    }

    protected override void OnSpawned(Pickup pickup)
    {
        GetOrCreateService(pickup.Serial);
        base.OnSpawned(pickup);
    }

    protected override void OnPickingUp(PickingUpItemEventArgs ev)
    {
        GetOrCreateService(ev.Pickup.Serial);
        ApplyNoPermissions(ev.Pickup);
        base.OnPickingUp(ev);
    }

    protected override void OnPickupAdded(Exiled.Events.EventArgs.Map.PickupAddedEventArgs ev)
    {
        GetOrCreateService(ev.Pickup.Serial);
        ApplyNoPermissions(ev.Pickup);
        ReapplyNoPermissionsDelayed(ev.Pickup);
        base.OnPickupAdded(ev);
    }

    protected override void OnSerialUntracked(ushort serial)
    {
        Services.Remove(serial);
        base.OnSerialUntracked(serial);
    }

    public override void RegisterEvents()
    {
        SNAPI.Events.Handlers.SnakePlayer.Score += OnSnakeScore;
        Exiled.Events.Handlers.Item.KeycardInteracting += OnKeycardInteracting;
        Exiled.Events.Handlers.Player.InteractingDoor += OnInteractingDoor;
        Exiled.Events.Handlers.Player.ChangingItem += OnAnyChangingItem;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        SNAPI.Events.Handlers.SnakePlayer.Score -= OnSnakeScore;
        Exiled.Events.Handlers.Item.KeycardInteracting -= OnKeycardInteracting;
        Exiled.Events.Handlers.Player.InteractingDoor -= OnInteractingDoor;
        Exiled.Events.Handlers.Player.ChangingItem -= OnAnyChangingItem;
        base.UnregisterEvents();
    }

    private void OnSnakeScore(ScoreEventArgs ev)
    {
        if (!TryGet(ev.Keycard, out var item) ||
            item is not AccessTunerBase owner ||
            !ReferenceEquals(owner, this))
        {
            return;
        }

        AccessTunerService device = owner.GetOrCreateService(ev.Keycard.Serial);
        ApplyNoPermissions(ev.Keycard);
        int gainedPoints = owner.AccessLevel switch
        {
            AccessTunerLevel.LevelOne => 5,
            AccessTunerLevel.LevelTwo => 10,
            AccessTunerLevel.LevelThree => 20,
            _ => 0
        };
        int maxValue = GetMaxPoints(owner.AccessLevel);

        device.TunePoints = Math.Min(maxValue, device.TunePoints + gainedPoints);
        owner.UpdateEffectedInfo(ev.Player, ev.Keycard.Serial);
    }

    private void OnKeycardInteracting(KeycardInteractingEventArgs ev)
    {
        if (!Check(ev.KeycardPickup)) return;

        ApplyNoPermissions(ev.KeycardPickup);
        if (TryHackDoor(ev.Player, ev.KeycardPickup.Serial, ev.Door.KeycardPermissions, out bool isAllowed))
            ev.IsAllowed = isAllowed;
    }

    private void OnInteractingDoor(InteractingDoorEventArgs ev)
    {
        if (!CheckHeld(ev.Player))
            return;

        if (ev.Door.IsLocked || CustomMapMainHandler.DoorAccess?.HasRuleAt(ev.Door) == true)
            return;

        Item? heldItem = ev.Player.CurrentItem;
        if (heldItem == null)
            return;

        ApplyNoPermissions(heldItem);
        if (TryHackDoor(ev.Player, heldItem.Serial, ev.Door.KeycardPermissions, out bool isAllowed))
            ev.IsAllowed = isAllowed;
    }

    private bool TryHackDoor(
        Player player,
        ushort serial,
        KeycardPermissions permissions,
        out bool isAllowed)
    {
        isAllowed = false;
        KeycardAccessLevels required = KeycardAccessLevels.FromPermissions(permissions);
        if (required is { Containment: 0, Armory: 0, Administration: 0 })
        {
            SetHackResult(player, serial, "ハック不要");
            return false;
        }

        AccessTunerService device = GetOrCreateService(serial);
        int requiredLevel = Math.Max(
            required.Containment,
            Math.Max(required.Armory, required.Administration));

        if (required.Containment > (int)device.AccessTunerLevel ||
            required.Armory > (int)device.AccessTunerLevel ||
            required.Administration > (int)device.AccessTunerLevel)
        {
            device.LastHackResult = $"失敗（権限不足: Lv.{requiredLevel}必要）";
            UpdateEffectedInfo(player, serial);
            return true;
        }

        int requiredPoints = GetRequiredPoints(requiredLevel);
        if (device.TunePoints < requiredPoints)
        {
            device.LastHackResult = $"失敗（ポイント不足: {requiredPoints}必要）";
            UpdateEffectedInfo(player, serial);
            return true;
        }

        device.TunePoints -= requiredPoints;
        device.UsedCounts++;
        device.LastHackResult = $"成功（Lv.{requiredLevel}扉）";
        UpdateEffectedInfo(player, serial);
        isAllowed = true;
        return true;
    }

    public bool TryConsumeSpecialDoorAccess(Player player)
    {
        if (AccessLevel != AccessTunerLevel.LevelThree || !CheckHeld(player))
            return false;

        Item? currentItem = player.CurrentItem;
        if (currentItem == null)
            return false;

        AccessTunerService device = GetOrCreateService(currentItem.Serial);
        if (device.AccessTunerLevel != AccessTunerLevel.LevelThree)
        {
            device.LastHackResult = "失敗（特殊扉権限不足）";
            UpdateEffectedInfo(player, currentItem.Serial);
            return false;
        }

        if (device.TunePoints < 20)
        {
            device.LastHackResult = "失敗（特殊扉には20ポイント必要）";
            UpdateEffectedInfo(player, currentItem.Serial);
            return false;
        }

        device.TunePoints -= 20;
        device.UsedCounts++;
        device.LastHackResult = "成功（特殊扉）";
        UpdateEffectedInfo(player, currentItem.Serial);
        return true;
    }

    protected override void OnChangingItem(ChangingItemEventArgs ev)
    {
        if (ev.IsAllowed && ev.Item != null)
            UpdateEffectedInfo(ev.Player, ev.Item.Serial);

        base.OnChangingItem(ev);
    }

    protected override void OnReleased(ItemRemovedEventArgs ev)
    {
        EffectedInfoTextProvider.Clear(ev.Player);
        base.OnReleased(ev);
    }

    protected override void OnOwnerDying(DyingEventArgs ev)
    {
        EffectedInfoTextProvider.Clear(ev.Player);
        base.OnOwnerDying(ev);
    }

    private void OnAnyChangingItem(ChangingItemEventArgs ev)
    {
        Item? currentItem = ev.Player.CurrentItem;
        if (currentItem == null || !Check(currentItem))
            return;

        if (ev.Item == null ||
            !TryGet(ev.Item, out var nextItem) ||
            nextItem is not AccessTunerBase)
        {
            EffectedInfoTextProvider.Clear(ev.Player);
        }
    }

    private void SetHackResult(Player player, ushort serial, string result)
    {
        AccessTunerService device = GetOrCreateService(serial);
        device.LastHackResult = result;
        UpdateEffectedInfo(player, serial);
    }

    private void UpdateEffectedInfo(Player player, ushort serial)
    {
        if (player == null)
            return;

        AccessTunerService device = GetOrCreateService(serial);
        int level = Math.Max(0, (int)device.AccessTunerLevel);
        int maxPoints = GetMaxPoints(device.AccessTunerLevel);
        int highestHackableLevel = GetHighestHackableLevel(device);
        string specialDoorStatus = device.AccessTunerLevel == AccessTunerLevel.LevelThree && device.TunePoints >= 20
            ? "<color=#88ff88>可能</color>"
            : "<color=#ff7777>不可</color>";

        string text =
            $"<color=#66ddff><b>ACCESS TUNER Lv.{level}</b></color>\n" +
            $"ポイント: <color=#ffee77>{device.TunePoints}/{maxPoints}</color>　使用回数: {device.UsedCounts}\n" +
            $"権限: C{level} / A{level} / AD{level}\n" +
            $"通常扉ハック: Lv.{highestHackableLevel}まで　特殊扉: {specialDoorStatus}\n" +
            $"直近の結果: {device.LastHackResult}";

        bool isHeld = player.CurrentItem?.Serial == serial && CheckHeld(player);
        EffectedInfoTextProvider.Set(player, text, isHeld ? 0f : 4f);
    }

    private AccessTunerService GetOrCreateService(ushort serial)
    {
        AccessTunerLevel resolvedLevel = NormalizeAccessLevel(AccessLevel);
        if (Services.TryGetValue(serial, out var service) &&
            service.AccessTunerLevel == resolvedLevel)
        {
            return service;
        }

        service = new AccessTunerService(resolvedLevel);
        Services[serial] = service;
        return service;
    }

    private static void ApplyNoPermissions(Item item)
    {
        if (item is Keycard keycard)
            keycard.Permissions = KeycardPermissions.None;
    }

    private static void ApplyNoPermissions(Pickup pickup)
    {
        if (pickup is KeycardPickup keycardPickup)
            keycardPickup.Permissions = KeycardPermissions.None;
    }

    private void ReapplyNoPermissionsDelayed(Item item)
    {
        ushort serial = item.Serial;
        Timing.CallDelayed(0.1f, () =>
        {
            if (!Check(item) || item.Serial != serial)
                return;

            ApplyNoPermissions(item);
        });
    }

    private void ReapplyNoPermissionsDelayed(Pickup pickup)
    {
        ushort serial = pickup.Serial;
        Timing.CallDelayed(0.1f, () =>
        {
            if (!Check(pickup) || pickup.Serial != serial || pickup.Base?.gameObject == null)
                return;

            ApplyNoPermissions(pickup);
        });
    }

    private AccessTunerLevel NormalizeAccessLevel(AccessTunerLevel level)
        => level switch
        {
            AccessTunerLevel.LevelOne => AccessTunerLevel.LevelOne,
            AccessTunerLevel.LevelTwo => AccessTunerLevel.LevelTwo,
            AccessTunerLevel.LevelThree => AccessTunerLevel.LevelThree,
            AccessTunerLevel.Broken => AccessTunerLevel.Broken,
            _ => AccessTunerLevel.Broken
        };

    private static int GetRequiredPoints(int level)
        => level switch
        {
            1 => 5,
            2 => 10,
            3 => 20,
            _ => 0
        };

    private static int GetMaxPoints(AccessTunerLevel level)
        => level switch
        {
            AccessTunerLevel.LevelOne => 25,
            AccessTunerLevel.LevelTwo => 50,
            AccessTunerLevel.LevelThree => 100,
            _ => 0
        };

    private static int GetHighestHackableLevel(AccessTunerService device)
    {
        int accessLevel = Math.Max(0, (int)device.AccessTunerLevel);
        if (accessLevel >= 3 && device.TunePoints >= 20) return 3;
        if (accessLevel >= 2 && device.TunePoints >= 10) return 2;
        if (accessLevel >= 1 && device.TunePoints >= 5) return 1;
        return 0;
    }

    protected string GetModelName(AccessTunerLevel accessTunerLevel)
    {
        string schematicName = accessTunerLevel switch
        {
            AccessTunerLevel.LevelOne => "Alienisolation_AccessTuner_Lv1",
            AccessTunerLevel.LevelTwo => "Alienisolation_AccessTuner_Lv2",
            AccessTunerLevel.LevelThree => "Alienisolation_AccessTuner_Lv3",
            AccessTunerLevel.Broken => "Alienisolation_AccessTuner_Broken",
            AccessTunerLevel.Undefined => "Alienisolation_AccessTuner_Broken",
            _ => "Alienisolation_AccessTuner_Broken",
        };
        return schematicName;
    }
}
