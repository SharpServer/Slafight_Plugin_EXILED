#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using MEC;
using Slafight_Plugin_EXILED.Extensions;
using PlayerEvents = Exiled.Events.EventArgs.Player;
using MapEvents = Exiled.Events.EventArgs.Map;
using Scp914Events = Exiled.Events.EventArgs.Scp914;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// 複数の CItem を「モード」として持ち、ServerSpecifics のアイテムモード切り替えキーで切り替えられる
/// 仮想カスタムアイテム基底クラス。
///
/// 設計メモ:
/// ─ 各 SubMode は独立した CItem であり、スタンドアロン利用も可能。
/// ─ モード切替は serial ごとの dispatch 先差し替えで実現し、CItem の
///   イベント購読・自動登録の仕組みとは完全に独立している。
/// ─ SerialToItem のエントリは CItemHybrid 自身（this）が保持する。
///   GetCurrentSub で現在モードの sub CItem にディスパッチする。
/// ─ serial ごとの <see cref="HybridState"/> が「現在モード」と「モードごとの保存状態」を
///   持つ。WaitingForPlayers でのみ一括クリアする。
///
/// 状態保持（モード切替で弾数が復活しないための核心）:
/// モードは別 ItemType（例: GunE11SR ↔ GunFRMG0）なので物理アイテムの差し替えは不可避。
/// ChangeMode は破棄前の旧アイテムの状態を <see cref="CItem.OnCaptureState"/> でスナップショットし、
/// HybridState.SavedStates に保存する。切替先のアイテム生成後は
///   1. sub の CustomizeItem を手動適用（生の AddItem では適用されないため）
///   2. 保存状態があれば OnRestoreState で上書き
/// の順で構成・状態を復元する。これにより各モードの弾数・アタッチメントが保持される。
///
/// 拾い直しバグ修正:
/// EXILED のイベント発火順（床から拾い直し時）:
///   1. PickingUpItem
///   2. ItemAdded     → OnAcquired が HybridState を維持/登録
///   3. PickupDestroyed
/// OnPickupDestroyed で HybridState を即削除すると 2 の維持が消える。
/// 修正: OnPickupDestroyed では削除しない。WaitingForPlayers で一括クリアする。
/// </summary>
public abstract class CItemHybrid : CItem
{
    private const float HybridSelectedHintDuration = 5f;

    /// <summary>serial ごとの Hybrid 状態（現在モード + モードごとの保存状態）。</summary>
    private sealed class HybridState
    {
        public int CurrentMode;

        // modeIndex → そのモードを離れたときの sub アイテム状態スナップショット
        public readonly Dictionary<int, object?> SavedStates = new();
    }

    // serial → HybridState。
    // ラウンド内でシリアルが生きている間は保持する。WaitingForPlayers でのみ一括クリア。
    private readonly Dictionary<ushort, HybridState> _states = new();

    // Give() / SwitchMode() の AddItem 直前に設定するモードインデックス。
    // ItemAdded → OnAcquired が _pendingModeIndex を読んで新 serial の初期モードを決める。
    private int _pendingModeIndex;

    // 切替時、新 serial に引き継ぐ既存 HybridState（SavedStates を保持したまま移送する）。
    private HybridState? _pendingState;

    // OnOwnerDying の重複呼び出し防止
    private PlayerEvents.DyingEventArgs? _lastDyingEv;

    // SwitchMode 実行中は Selected Hint を抑制する
    private bool _isSwitching;

    private List<CItemHybridMode>? _subModes;
    protected List<CItemHybridMode> SubModes => _subModes ??= BuildSubModes();

    /// <summary>モードとして使う CItem インスタンスのリストを構築する。インデックス 0 が初期モード。</summary>
    protected abstract List<CItemHybridMode> BuildSubModes();

    protected override ItemType BaseItem => SubModes[0].TargetItem.GetBaseItem();

    /// <summary>ServerSpecifics のキー入力によるモード切替を許可するか。</summary>
    protected virtual bool EnableKeyModeSwitch => true;

    /// <summary>モード切替時に Hint を表示するか。</summary>
    protected virtual bool ShowModeSwitchHint => true;

    // ======================================================
    // Give
    // ======================================================

    public override Item? Give(Player? player, bool displayMessage = false)
    {
        _pendingModeIndex = 0;
        _pendingState     = null;
        var item = base.Give(player, displayMessage);
        // HybridState の登録は ItemAdded → OnAcquired で完結する。
        return item;
    }

    // ======================================================
    // モード切替
    // ======================================================

    /// <summary>
    /// 指定 serial のアイテムを次のモードへ切り替える。
    /// 旧アイテムの状態をスナップショットしてから破棄し、新 ItemType のアイテムを追加して
    /// 構成・保存状態を復元し、自動持ち替えする。インベントリが満杯の場合はキャンセルする。
    /// </summary>
    public void SwitchMode(ushort oldSerial, Player player)
        => ChangeMode(oldSerial, player);

    /// <summary>キー入力からのモード切替。EnableKeyModeSwitch が false の場合は何もしない。</summary>
    public bool TrySwitchModeFromInput(ushort oldSerial, Player player)
    {
        if (!EnableKeyModeSwitch)
        {
            Log.Debug($"[Hybrid] TrySwitchModeFromInput: key mode switch disabled for {GetType().Name}");
            return false;
        }

        ChangeMode(oldSerial, player);
        return true;
    }

    /// <summary>現在手に持っている Hybrid アイテムを次のモードへ切り替える。</summary>
    protected void ChangeMode(Player player)
    {
        var item = player.CurrentItem;
        if (item == null)
        {
            Log.Debug($"[Hybrid] ChangeMode: no CurrentItem for {player.Nickname}");
            return;
        }

        ChangeMode(item.Serial, player);
    }

    /// <summary>指定 serial の Hybrid アイテムを次のモードへ切り替える。</summary>
    protected void ChangeMode(ushort oldSerial, Player player)
    {
        if (!_states.TryGetValue(oldSerial, out var state))
        {
            Log.Debug($"[Hybrid] SwitchMode: no state for serial={oldSerial}");
            return;
        }

        var oldItem = player.Items.FirstOrDefault(i => i?.Serial == oldSerial);
        if (oldItem == null)
        {
            Log.Debug($"[Hybrid] SwitchMode: player has no item with serial={oldSerial}");
            return;
        }

        int currentIndex = state.CurrentMode;
        int nextIndex    = (currentIndex + 1) % SubModes.Count;
        var currentSub   = SubModes[currentIndex].TargetItem;
        var nextSub      = SubModes[nextIndex].TargetItem;

        Log.Debug($"[Hybrid] SwitchMode: serial={oldSerial} mode {currentIndex} -> {nextIndex}");

        // 1. 破棄前に旧アイテムの状態をスナップショットして保存する。
        state.SavedStates[currentIndex] = currentSub.CallOnCaptureState(oldItem);

        // 2. 新 serial へ引き継ぐ state を準備（SavedStates ごと移送）。
        state.CurrentMode = nextIndex;
        _pendingState     = state;
        _pendingModeIndex = nextIndex;
        SetPendingGive(this, false);

        Item? newItem;
        try
        {
            newItem = player.AddItem(nextSub.GetBaseItem());
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
            _pendingState     = null;
        }

        if (newItem == null)
        {
            Log.Debug("[Hybrid] SwitchMode: AddItem returned null (inventory full?)");
            // 切替失敗。state は oldSerial のまま現在モードへ戻す。
            state.CurrentMode = currentIndex;
            return;
        }

        // 3. 構成を適用する。生の AddItem では sub の CustomizeItem を通らないため、
        //    アタッチメント・容量・初期弾数をここで焼き付ける。
        nextSub.CallCustomizeItem(newItem);

        // 4. 新アイテムを手に持たせる（切り替えアニメーション）
        _isSwitching = true;
        try { player.CurrentItem = newItem; }
        finally { _isSwitching = false; }

        // 5. 保存状態があれば復元（無ければ初回入場 = CustomizeItem の初期弾数のまま）。
        //    BarrelMagazine 等は装備後でないと初期化されない場合があるため、復元は持ち替え後に行う。
        if (state.SavedStates.TryGetValue(nextIndex, out var saved) && saved != null)
            nextSub.CallOnRestoreState(newItem, saved);

        // 6. レディ化。強制持ち替えで自動火器が未 cock のまま残り発射不能になるのを補正する
        //    （初回入場・復元のどちらでも必要）。
        nextSub.CallOnModeActivated(newItem);

        // 7. 旧 serial を掃除してから旧アイテムを削除する。
        //    RemoveItem は ItemRemoved を同期発火するので、先に ForceUnregister して
        //    OnReleased が旧 serial に誤ってディスパッチされないようにする。
        _states.Remove(oldSerial);
        SerialTracker.ForceUnregister(oldSerial);
        player.RemoveItem(oldItem, destroy: true);

        if (ShowModeSwitchHint)
        {
            player.ShowHint($"<size=24>モード切替: {SubModes[nextIndex].ModeName.OrDefault(nextSub.DisplayName)}</size>\n" +
                $"<size=23>{SubModes[nextIndex].ModeDescription.OrDefault(nextSub.Description)}</size>",
                2f);
        }
    }

    // ======================================================
    // RebindSerialFor（ロード等でシリアルの再紐付けが必要なとき）
    // ======================================================

    /// <summary>
    /// 指定アイテムのシリアルが _states に無ければ初期モード 0 で再登録する。
    /// ロード後のリバインドや、外部からの AddItem 経由で渡ってきたアイテムの救済用。
    /// </summary>
    public void RebindSerialFor(Item item, Player owner)
    {
        if (_states.ContainsKey(item.Serial)) return;

        _states[item.Serial] = new HybridState { CurrentMode = 0 };
        Log.Debug($"[Hybrid] RebindSerialFor: owner={owner.Nickname} serial={item.Serial} modeIndex=0");
    }

    // ======================================================
    // sub 解決
    // ======================================================

    private CItem? GetCurrentSub(ushort serial)
    {
        if (!_states.TryGetValue(serial, out var state)) return null;
        int idx = state.CurrentMode;
        if (idx < 0 || idx >= SubModes.Count)
        {
            Log.Warn($"[Hybrid] GetCurrentSub: serial={serial} idx={idx} out of range (SubModes.Count={SubModes.Count})");
            return null;
        }
        return SubModes[idx].TargetItem;
    }

    internal bool IsCurrentSub(ushort serial, CItem sub)
    {
        var current = GetCurrentSub(serial);
        return current != null && ReferenceEquals(current, sub);
    }

    // ======================================================
    // Debug HUD 用
    // ======================================================

    public string GetDebugStateFor(Player player, ushort currentSerial)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<color=#88ffcc>[Hybrid]</color>");

        if (_states.TryGetValue(currentSerial, out var state))
        {
            var modeName = GetModeName(state.CurrentMode);
            var savedKeys = state.SavedStates.Count == 0
                ? "-"
                : string.Join(",", state.SavedStates.Keys.OrderBy(k => k));
            sb.AppendLine(
                $"  <color=#aaaaaa>CurrentSerial:</color> {currentSerial}  " +
                $"<color=#aaaaaa>ModeIndex:</color> {state.CurrentMode}  " +
                $"<color=#aaaaaa>Name:</color> {modeName}  " +
                $"<color=#aaaaaa>Saved:</color> {savedKeys}");
        }
        else
        {
            sb.AppendLine($"  <color=#ff4444>CurrentSerial {currentSerial} has no state.</color>");
        }

        if (_states.Count == 0)
        {
            sb.AppendLine("  <color=#666666>SerialMap: (empty)</color>");
        }
        else
        {
            sb.AppendLine("  <color=#aaaaaa>SerialMap:</color>");
            foreach (var kv in _states.OrderBy(kv => kv.Key))
            {
                sb.AppendLine(
                    $"    - Serial={kv.Key}  Index={kv.Value.CurrentMode}  Name={GetModeName(kv.Value.CurrentMode)}");
            }
        }

        return sb.ToString();
    }

    private string GetModeName(int idx)
        => (idx >= 0 && idx < SubModes.Count)
            ? SubModes[idx]?.ModeName.OrDefault(SubModes[idx]?.TargetItem.DisplayName ?? $"Mode#{idx}") ?? $"Mode#{idx}"
            : $"(out of range: {idx})";

    // ======================================================
    // CItem virtual overrides
    // ======================================================

    protected override void OnAcquired(PlayerEvents.ItemAddedEventArgs ev, bool displayMessage)
    {
        Log.Debug($"[Hybrid] OnAcquired: ci={GetType().Name} owner={ev.Player.Nickname} serial={ev.Item.Serial} pending={_pendingModeIndex}");

        // まだ登録されていなければ登録。既に登録済み（拾い直し等）ならモードを維持する。
        if (!_states.ContainsKey(ev.Item.Serial))
        {
            if (_pendingState != null)
            {
                // 切替経路: SavedStates ごと state を引き継ぐ。
                _states[ev.Item.Serial] = _pendingState;
                Log.Debug($"[Hybrid] OnAcquired: carry state modeIndex={_pendingState.CurrentMode} for serial={ev.Item.Serial}");
            }
            else
            {
                int idx = _pendingModeIndex;
                if (idx < 0 || idx >= SubModes.Count) idx = 0;

                _states[ev.Item.Serial] = new HybridState { CurrentMode = idx };
                Log.Debug($"[Hybrid] OnAcquired: set modeIndex={idx} for serial={ev.Item.Serial}");
            }
        }
        else
        {
            Log.Debug($"[Hybrid] OnAcquired: keep modeIndex={_states[ev.Item.Serial].CurrentMode} for serial={ev.Item.Serial}");
        }

        GetCurrentSub(ev.Item.Serial)?.CallOnAcquired(ev, displayMessage);
    }

    protected override void OnReleased(PlayerEvents.ItemRemovedEventArgs ev)
        => GetCurrentSub(ev.Item.Serial)?.CallOnReleased(ev);

    protected override void OnSpawned(Pickup pickup)
    {
        // ドロップ時: _states にすでにモードが入っているはずなので上書きしない。
        // Spawn() で直接生成された場合（シリアル初登場）のみ 0 で初期化する。
        if (!_states.ContainsKey(pickup.Serial))
        {
            _states[pickup.Serial] = new HybridState { CurrentMode = 0 };
            Log.Debug($"[Hybrid] OnSpawned: pickup serial={pickup.Serial} modeIndex=0 (new)");
        }

        GetCurrentSub(pickup.Serial)?.CallOnSpawned(pickup);
    }

    protected override void OnPickingUp(PlayerEvents.PickingUpItemEventArgs ev)
        => GetCurrentSub(ev.Pickup.Serial)?.CallOnPickingUp(ev);

    protected override void OnDropping(PlayerEvents.DroppingItemEventArgs ev)
        => GetCurrentSub(ev.Item.Serial)?.CallOnDropping(ev);

    protected override void OnUsing(PlayerEvents.UsingItemEventArgs ev)
        => GetCurrentSub(ev.Item.Serial)?.CallOnUsing(ev);

    protected override void OnUsingItemCompleted(PlayerEvents.UsingItemCompletedEventArgs ev)
        => GetCurrentSub(ev.Item.Serial)?.CallOnUsingItemCompleted(ev);

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
        // Dying は所持アイテム数分ループで呼ばれるが、同一イベントへの重複呼び出しを防ぐ。
        if (ReferenceEquals(_lastDyingEv, ev)) return;
        _lastDyingEv = ev;

        var notified = new HashSet<int>();
        foreach (var item in ev.Player.Items)
        {
            if (item == null) continue;
            if (!TryGet(item.Serial, out var ci) || !ReferenceEquals(ci, this)) continue;
            if (!_states.TryGetValue(item.Serial, out var state)) continue;
            if (notified.Add(state.CurrentMode))
                SubModes[state.CurrentMode]?.TargetItem.CallOnOwnerDying(ev);
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

        // ─────────────────────────────────────────────────────────────────
        // 重要: _states をここでは削除しない。
        //
        // EXILED のイベント発火順（床 → インベントリ = 拾い直し）:
        //   1. PickingUpItem
        //   2. ItemAdded → OnAcquired: _states[serial] を維持/登録
        //   3. PickupDestroyed ← ★ここ
        //
        // 手順 2 で OnAcquired が状態を維持しているため、
        // 手順 3 で削除すると直後の GetCurrentSub が null を返す（= 旧バグの原因）。
        //
        // 純粋消滅（銃やグレネードによる Pickup 破壊 / ラウンド終了時 Cleanup）の場合も
        // 同様に削除しない。WaitingForPlayers が一括クリアするので問題ない。
        // ─────────────────────────────────────────────────────────────────
    }

    protected override void OnSerialUntracked(ushort serial)
    {
        _states.Remove(serial);
    }

    protected override void OnUpgradingPickup(Scp914Events.UpgradingPickupEventArgs ev)
        => GetCurrentSub(ev.Pickup.Serial)?.CallOnUpgradingPickup(ev);

    protected override void OnUpgradingInventoryItem(Scp914Events.UpgradingInventoryItemEventArgs ev)
        => GetCurrentSub(ev.Item.Serial)?.CallOnUpgradingInventoryItem(ev);

    protected override void CustomizeItem(Item item)
    {
        var idx = _states.TryGetValue(item.Serial, out var state) ? state.CurrentMode : _pendingModeIndex;
        if (idx >= 0 && idx < SubModes.Count)
            SubModes[idx]?.TargetItem.CallCustomizeItem(item);
        base.CustomizeItem(item);
    }

    protected override void CustomizePickup(Pickup pickup)
    {
        var idx = _states.TryGetValue(pickup.Serial, out var state) ? state.CurrentMode : _pendingModeIndex;
        if (idx >= 0 && idx < SubModes.Count)
            SubModes[idx]?.TargetItem.CallCustomizePickup(pickup);
        base.CustomizePickup(pickup);
    }

    protected override void ShowSelectedMessage(Player player)
    {
        // SwitchMode 実行中はモード切替 Hint を優先するので Selected Hint を抑制
        if (_isSwitching) return;

        if (player == null) return;

        var hint = BuildHybridSelectedMessage(player);
        player.ShowHint(hint.Text, hint.Parameters, null, HybridSelectedHintDuration);
        var captured = player;
        Timing.CallDelayed(HybridSelectedHintDuration, () =>
        {
            try { OnSelectedHintFinished(captured); }
            catch (Exception e) { Log.Error($"CItemHybrid.OnSelectedHintFinished error in {GetType().Name}: {e}"); }
        });
    }

    private ServerSpecificUserSettings.KeybindHintContent BuildHybridSelectedMessage(Player player)
    {
        string message = BuildSelectedMessage();

        if (!EnableKeyModeSwitch || SubModes.Count <= 1)
            return new ServerSpecificUserSettings.KeybindHintContent(message, []);

        var keybindHint = ServerSpecificUserSettings.BuildKeybindUsageHint(
            player,
            ServerSpecifics.ItemModeSwitchKeybindSettingId,
            "モードを切り替えられます");

        return new ServerSpecificUserSettings.KeybindHintContent(
            message + "\n" + keybindHint.Text,
            keybindHint.Parameters);
    }

    // ShowPickedUpMessage は基底に任せる（override 不要）

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
        // ラウンド間: 全シリアルをクリア（CItem 基底の SerialToItem クリアと一致）
        _states.Clear();
        _lastDyingEv = null;

        foreach (var sub in SubModes)
            sub?.TargetItem.CallOnWaitingForPlayers();
    }
}

// ======================================================
// CItemHybridMode (モード定義)
// ======================================================

public class CItemHybridMode(
    CItem  targetItem,
    string modeName        = "",
    string modeDescription = "")
{
    public CItem  TargetItem       { get; set; } = targetItem;
    public string ModeName         { get; set; } = modeName;
    public string ModeDescription  { get; set; } = modeDescription;
}
