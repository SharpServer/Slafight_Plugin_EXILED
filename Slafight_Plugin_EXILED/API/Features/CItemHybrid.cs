#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;

using PlayerEvents = Exiled.Events.EventArgs.Player;
using MapEvents = Exiled.Events.EventArgs.Map;
using Scp914Events = Exiled.Events.EventArgs.Scp914;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// 複数の CItem を「モード」として持ち、G キー（ServerSpecifics ID 5）で切り替えられる
/// 仮想カスタムアイテム基底クラス。
/// 各 SubMode は独立した CItem であり、スタンドアロン利用も可能。
/// モード切替は serial ごとの dispatch先差し替えで実現し、CItem の
/// イベント購読・自動登録の仕組みとは完全に独立している。
/// </summary>
public abstract class CItemHybrid : CItem
{
    // serial → 現在のモードインデックス
    private readonly Dictionary<ushort, int> _serialModeIndex = new();

    // 次の AddItem 操作に割り当てるモードインデックス（Give / SwitchMode から設定）
    private int _pendingModeIndex;

    // OnOwnerDying の重複呼び出し防止
    private PlayerEvents.DyingEventArgs? _lastDyingEv;

    // SwitchMode 実行中は selected hint を抑制するフラグ
    private bool _isSwitching;

    private List<CItemHybridMode>? _subModes;
    protected List<CItemHybridMode> SubModes => _subModes ??= BuildSubModes();

    /// <summary>モードとして使う CItem インスタンスのリストを構築する。インデックス 0 が初期モード。</summary>
    protected abstract List<CItemHybridMode> BuildSubModes();

    protected override ItemType BaseItem => SubModes[0].TargetItem.GetBaseItem();

    // ==== Give ====

    public override Item? Give(Player? player, bool displayMessage = false)
    {
        _pendingModeIndex = 0;
        // CItem.Give が _pendingGiveCItem をセットしてくれるのでそのまま使う
        var item = base.Give(player, displayMessage);

        // 念のため modeIndex を初期化（ItemAdded が来る前に serial は分からないのでここでは触らない）
        return item;
    }

    // ==== Debug HUD 用 ====

    public string GetDebugStateFor(Player player, ushort currentSerial)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("<color=#88ffcc>[Hybrid]</color>");

        if (_serialModeIndex.TryGetValue(currentSerial, out var value))
        {
            string modeName = (value >= 0 && value < SubModes.Count)
                ? SubModes[value]?.ModeName ?? SubModes[value]?.TargetItem.DisplayName ?? $"Mode#{value}"
                : $"(out of range: {value})";

            sb.AppendLine(
                $"  <color=#aaaaaa>CurrentSerial:</color> {currentSerial}  " +
                $"<color=#aaaaaa>ModeIndex:</color> {value}  " +
                $"<color=#aaaaaa>Name:</color> {modeName}"
            );
        }
        else
        {
            sb.AppendLine(
                $"  <color=#ff4444>CurrentSerial {currentSerial} has no mode index.</color>"
            );
        }

        if (_serialModeIndex.Count == 0)
        {
            sb.AppendLine("  <color=#666666>SerialMap: (empty)</color>");
        }
        else
        {
            sb.AppendLine("  <color=#aaaaaa>SerialMap:</color>");
            foreach (var kv in _serialModeIndex.OrderBy(kv => kv.Key))
            {
                ushort serial = kv.Key;
                int idx = kv.Value;

                string modeName = (idx >= 0 && idx < SubModes.Count)
                    ? SubModes[idx]?.ModeName ?? SubModes[idx]?.TargetItem.DisplayName ?? $"Mode#{idx}"
                    : $"(out of range: {idx})";

                sb.AppendLine(
                    $"    - Serial={serial}  Index={idx}  Name={modeName}"
                );
            }
        }

        return sb.ToString();
    }
    
    public void RebindSerialFor(Item item, Player owner)
    {
        // すでにモードが付いていれば何もしない
        if (_serialModeIndex.ContainsKey(item.Serial))
            return;

        // ここでは「初期モード 0」に戻すシンプルな挙動にする
        _serialModeIndex[item.Serial] = 0;

        Log.Debug($"[Hybrid] RebindSerialFor: owner={owner.Nickname}, serial={item.Serial}, modeIndex=0");
    }

    // ==== モード切替 ====

    /// <summary>
    /// 指定 serial のアイテムを次のモードへ切り替える。
    /// 旧アイテムを破棄し、新 ItemType のアイテムを追加して自動持ち替えする。
    /// インベントリが満杯の場合は切り替えをキャンセルする。
    /// </summary>
    public void SwitchMode(ushort oldSerial, Player player)
    {
        if (!_serialModeIndex.TryGetValue(oldSerial, out var currentIndex))
        {
            Log.Debug($"[Hybrid] SwitchMode: no mode index for serial={oldSerial}");
            return;
        }

        var oldItem = player.Items.FirstOrDefault(i => i?.Serial == oldSerial);
        if (oldItem == null)
        {
            Log.Debug($"[Hybrid] SwitchMode: player has no item with serial={oldSerial}");
            return;
        }

        int nextIndex = (currentIndex + 1) % SubModes.Count;
        var nextSub = SubModes[nextIndex];

        Log.Debug($"[Hybrid] SwitchMode: {oldSerial} mode {currentIndex} -> {nextIndex}");

        // AddItem 前に pending を設定することで、ItemAdded が Hybrid singleton を追跡する
        _pendingModeIndex = nextIndex;
        SetPendingGive(this, false);
        Item? newItem;
        try
        {
            newItem = player.AddItem(nextSub.TargetItem.GetBaseItem());
        }
        catch (Exception e)
        {
            Log.Error($"[Hybrid] SwitchMode AddItem error: {e}");
            newItem = null;
        }
        finally
        {
            ClearPendingGive();
            _pendingModeIndex = 0;
        }

        if (newItem == null)
        {
            Log.Debug("[Hybrid] SwitchMode: AddItem returned null (inventory full?)");
            return; // インベントリ満杯
        }

        // CItem 側の ItemAdded で SerialToItem は既に this に差し替わっているはず

        // CurrentItem を切り替える時点では oldSerial がまだ SerialToItem に残っている。
        _isSwitching = true;
        try { player.CurrentItem = newItem; }
        finally { _isSwitching = false; }

        // ChangingItem が解決した後に旧 serial を解除
        _serialModeIndex.Remove(oldSerial);
        SerialTracker.ForceUnregister(oldSerial);

        // serial 解除後に RemoveItem → ItemRemoved の dispatch をスキップ
        player.RemoveItem(oldItem, destroy: true);

        player.ShowHint(
            $"<size=24>モード切替: {nextSub.ModeName.OrDefault($"{nextSub.TargetItem.DisplayName}")}</size>\n" +
            $"<size=23>{nextSub.ModeDescription.OrDefault($"{nextSub.TargetItem.Description}")}</size>",
            2f);
    }

    // ==== sub 解決 ====

    private CItem? GetCurrentSub(ushort serial)
    {
        if (!_serialModeIndex.TryGetValue(serial, out var idx)) return null;
        if (idx < 0 || idx >= SubModes.Count) return null;
        return SubModes[idx].TargetItem;
    }

    internal bool IsCurrentSub(ushort serial, CItem sub)
    {
        var current = GetCurrentSub(serial);
        return current != null && ReferenceEquals(current, sub);
    }

    // ==== CItem virtual overrides ====

    protected override void OnAcquired(PlayerEvents.ItemAddedEventArgs ev, bool displayMessage)
    {
        Log.Debug($"[Hybrid] OnAcquired: ci={GetType().Name} owner={ev.Player.Nickname} serial={ev.Item.Serial} pending={_pendingModeIndex}");

        if (!_serialModeIndex.ContainsKey(ev.Item.Serial))
        {
            int idx = _pendingModeIndex;
            if (idx < 0 || idx >= SubModes.Count)
                idx = 0;

            _serialModeIndex[ev.Item.Serial] = idx;
            Log.Debug($"[Hybrid] OnAcquired: set modeIndex={idx} for serial={ev.Item.Serial}");
        }
        else
        {
            Log.Debug($"[Hybrid] OnAcquired: keep modeIndex={_serialModeIndex[ev.Item.Serial]} for serial={ev.Item.Serial}");
        }

        GetCurrentSub(ev.Item.Serial)?.CallOnAcquired(ev, displayMessage);
    }

    protected override void OnReleased(PlayerEvents.ItemRemovedEventArgs ev)
        => GetCurrentSub(ev.Item.Serial)?.CallOnReleased(ev);

    protected override void OnSpawned(Pickup pickup)
    {
        if (!_serialModeIndex.ContainsKey(pickup.Serial))
        {
            _serialModeIndex[pickup.Serial] = 0;
            Log.Debug($"[Hybrid] OnSpawned: pickup serial={pickup.Serial}, modeIndex=0");
        }

        GetCurrentSub(pickup.Serial)?.CallOnSpawned(pickup);
    }

    protected override void OnPickingUp(PlayerEvents.PickingUpItemEventArgs ev)
        => GetCurrentSub(ev.Pickup.Serial)?.CallOnPickingUp(ev);

    protected override void OnDropping(PlayerEvents.DroppingItemEventArgs ev)
        => GetCurrentSub(ev.Item.Serial)?.CallOnDropping(ev);

    protected override void OnUsing(PlayerEvents.UsingItemEventArgs ev)
        => GetCurrentSub(ev.Item.Serial)?.CallOnUsing(ev);

    protected override void OnUsed(PlayerEvents.UsedItemEventArgs ev)
        => GetCurrentSub(ev.Item.Serial)?.CallOnUsed(ev);

    protected override void OnShooting(PlayerEvents.ShootingEventArgs ev)
    {
        var serial = ev.Player?.CurrentItem?.Serial;
        if (serial == null) return;
        GetCurrentSub(serial.Value)?.CallOnShooting(ev);
    }

    protected override void OnShot(PlayerEvents.ShotEventArgs ev)
    {
        var serial = ev.Player?.CurrentItem?.Serial;
        if (serial == null) return;
        GetCurrentSub(serial.Value)?.CallOnShot(ev);
    }

    protected override void OnHurtingOthers(PlayerEvents.HurtingEventArgs ev)
    {
        var serial = ev.Attacker?.CurrentItem?.Serial;
        if (serial == null) return;
        GetCurrentSub(serial.Value)?.CallOnHurtingOthers(ev);
    }

    protected override void OnOwnerDying(PlayerEvents.DyingEventArgs ev)
    {
        if (ReferenceEquals(_lastDyingEv, ev)) return;
        _lastDyingEv = ev;

        var notified = new HashSet<int>();
        foreach (var item in ev.Player.Items)
        {
            if (item == null) continue;
            if (!TryGet(item.Serial, out var ci) || !ReferenceEquals(ci, this)) continue;
            if (!_serialModeIndex.TryGetValue(item.Serial, out var idx)) continue;
            if (notified.Add(idx))
                SubModes[idx]?.TargetItem.CallOnOwnerDying(ev);
        }
    }

    protected override void OnChangingItem(PlayerEvents.ChangingItemEventArgs ev)
        => GetCurrentSub(ev.Item.Serial)?.CallOnChangingItem(ev);

    protected override void OnThrowingRequest(PlayerEvents.ThrowingRequestEventArgs ev)
        => GetCurrentSub(ev.Item.Serial)?.CallOnThrowingRequest(ev);

    protected override void OnPickupAdded(MapEvents.PickupAddedEventArgs ev)
        => GetCurrentSub(ev.Pickup.Serial)?.CallOnPickupAdded(ev);

    protected override void OnPickupDestroyed(MapEvents.PickupDestroyedEventArgs ev)
    {
        GetCurrentSub(ev.Pickup.Serial)?.CallOnPickupDestroyed(ev);
        _serialModeIndex.Remove(ev.Pickup.Serial);
    }

    public override void RegisterEvents()
    {
        foreach (var sub in SubModes)
            sub?.TargetItem.RegisterEvents();
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        foreach (var sub in SubModes)
            sub?.TargetItem.UnregisterEvents();
        base.UnregisterEvents();
    }

    protected override void OnWaitingForPlayers()
    {
        _serialModeIndex.Clear();
        _lastDyingEv = null;
        foreach (var sub in SubModes)
            sub?.TargetItem.CallOnWaitingForPlayers();
    }

    protected override void OnUpgradingPickup(Scp914Events.UpgradingPickupEventArgs ev)
        => GetCurrentSub(ev.Pickup.Serial)?.CallOnUpgradingPickup(ev);

    protected override void OnUpgradingInventoryItem(Scp914Events.UpgradingInventoryItemEventArgs ev)
        => GetCurrentSub(ev.Item.Serial)?.CallOnUpgradingInventoryItem(ev);

    protected override void CustomizeItem(Item item)
    {
        var idx = _serialModeIndex.TryGetValue(item.Serial, out var i) ? i : _pendingModeIndex;
        if (idx >= 0 && idx < SubModes.Count)
            SubModes[idx]?.TargetItem.CallCustomizeItem(item);
        base.CustomizeItem(item);
    }

    protected override void ShowPickedUpMessage(Player player)
        => base.ShowPickedUpMessage(player);

    protected override void ShowSelectedMessage(Player player)
    {
        if (_isSwitching) return;
        base.ShowSelectedMessage(player);
    }
}

public class CItemHybridMode(CItem targetItem, string modeName = "", string modeDescription = "")
{
    public CItem TargetItem { get; set; } = targetItem;
    public string ModeName { get; set; } = modeName;
    public string ModeDescription { get; set; } = modeDescription;
}