using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.Scp914Events;
using LabApi.Events.Arguments.ServerEvents;
using UnityEngine;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;

using ExiledItem = Exiled.API.Features.Items.Item;
using LabItem = LabApi.Features.Wrappers.Item;
using LabPickup = LabApi.Features.Wrappers.Pickup;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// ひとつの物理アイテムを複数の <see cref="CustomItem"/> モードで共有する基底です。
/// モード切替では物理アイテムだけを差し替え、現在モードと各モードの保存状態は
/// シリアル単位で引き継ぎます。
/// </summary>
/// <remarks>
/// <para>
/// 各モードは通常の <see cref="CustomItem"/> として実装できます。Hybrid がイベント、
/// カスタマイズ、拾得・投棄のライフサイクルを現在モードへ中継するため、モード側を
/// 静的イベントへ個別登録する必要はありません。
/// </para>
/// <para>
/// 状態を保存したいモードは <see cref="CustomItem.OnCaptureState"/> と
/// <see cref="CustomItem.OnRestoreState"/> を override してください。
/// <see cref="CustomFirearm"/> の弾数は基底側で自動的に保存・復元されます。
/// </para>
/// </remarks>
public abstract class CustomHybrid : CustomItem
{
    private const float ModeSwitchHintDuration = 2f;

    private sealed class HybridState
    {
        public int CurrentMode;
        public readonly Dictionary<int, object> SavedStates = new Dictionary<int, object>();
    }

    private readonly Dictionary<ushort, HybridState> states = new Dictionary<ushort, HybridState>();
    private readonly HashSet<int> startedModes = new HashSet<int>();

    private List<CustomHybridMode> subModes;
    private int pendingModeIndex;
    private HybridState pendingState;
    private bool changingMode;
    private bool switching;

    /// <summary>モード定義です。リストの 0 番目が初期モードになります。</summary>
    protected List<CustomHybridMode> SubModes => subModes ??= CreateSubModes();

    /// <summary>Hybrid のモードを構築します。</summary>
    protected abstract List<CustomHybridMode> BuildSubModes();

    /// <summary>Server Specifics のキー入力でモード切替を許可するか。</summary>
    protected virtual bool EnableKeyModeSwitch => true;

    /// <summary>モード切替時に短い通知を表示するか。</summary>
    protected virtual bool ShowModeSwitchHint => true;

    public override ItemType BaseType => SubModes.Count == 0 ? ItemType.None : SubModes[0].TargetItem.BaseType;

    // Hybrid 自身の表示は現在モードの定義を既定値にする。Hybrid 側で明示 override も可能。
    public override string Name => CurrentMode()?.TargetItem.Name ?? base.Name;
    public override string Description => CurrentMode()?.TargetItem.Description ?? base.Description;
    public override bool IgnoreCategoryLimits => CurrentMode()?.TargetItem.IgnoreCategoryLimits ?? base.IgnoreCategoryLimits;
    public override Rarity Rarity => CurrentMode()?.TargetItem.Rarity ?? base.Rarity;

    protected override bool PickupLightEnabled => CurrentMode()?.TargetItem.CallPickupLightEnabled() ?? base.PickupLightEnabled;
    protected override Color PickupLightColor => CurrentMode()?.TargetItem.CallPickupLightColor() ?? base.PickupLightColor;
    protected override float PickupLightIntensity => CurrentMode()?.TargetItem.CallPickupLightIntensity() ?? base.PickupLightIntensity;
    protected override float PickupLightRange => CurrentMode()?.TargetItem.CallPickupLightRange() ?? base.PickupLightRange;
    protected override LightShadows PickupLightShadowType => CurrentMode()?.TargetItem.CallPickupLightShadowType() ?? base.PickupLightShadowType;
    protected override string PickupSchematicName => CurrentMode()?.TargetItem.CallPickupSchematicName() ?? base.PickupSchematicName;
    protected override Vector3 PickupSchematicScale => CurrentMode()?.TargetItem.CallPickupSchematicScale() ?? base.PickupSchematicScale;

    protected override string PickupHint
        => CurrentMode()?.TargetItem.CallPickupHint() ?? base.PickupHint;

    protected override float PickupHintDuration
        => CurrentMode()?.TargetItem.CallPickupHintDuration() ?? base.PickupHintDuration;

    protected override string SelectedHint
    {
        get
        {
            if (switching) return string.Empty;

            string message = CurrentMode()?.TargetItem.CallSelectedHint() ?? base.SelectedHint;
            if (!EnableKeyModeSwitch || SubModes.Count <= 1)
                return message;

            string suggested = ServerSpecificUserSettings.GetSuggestedKeyText(ServerSpecifics.ItemModeSwitchKeybindSettingId);
            string keyText = string.IsNullOrEmpty(suggested) ? "Server Specifics の設定キー" : suggested;
            return message + $"\n<size=22><color=#aaaaaa>{keyText}</color> でモードを切り替えられます。</size>";
        }
    }

    protected override float SelectedHintDuration
        => CurrentMode()?.TargetItem.CallSelectedHintDuration() ?? base.SelectedHintDuration;

    /// <summary>指定 serial の Hybrid を次のモードへ切り替えます。</summary>
    public void SwitchMode(ushort serial, Player player) => ChangeMode(serial, player);

    /// <summary>現在手に持っている Hybrid を次のモードへ切り替えます。</summary>
    protected void ChangeMode(Player player)
    {
        if (player?.CurrentItem is { } item)
            ChangeMode(item.Serial, player);
    }

    /// <summary>キー入力から切り替えます。無効化されている場合は false を返します。</summary>
    public bool TrySwitchModeFromInput(ushort serial, Player player)
    {
        if (!EnableKeyModeSwitch)
            return false;

        ChangeMode(serial, player);
        return true;
    }

    /// <summary>
    /// 指定した serial の物理アイテムを次のモードへ差し替えます。
    /// インベントリが満杯、または対象が見つからない場合は何もしません。
    /// </summary>
    protected void ChangeMode(ushort oldSerial, Player player)
    {
        if (changingMode) return;

        changingMode = true;
        try
        {
            ChangeModeCore(oldSerial, player);
        }
        finally
        {
            changingMode = false;
            switching = false;
            pendingState = null;
            pendingModeIndex = 0;
        }
    }

    private void ChangeModeCore(ushort oldSerial, Player player)
    {
        if (player == null || !states.TryGetValue(oldSerial, out HybridState state))
            return;

        ExiledItem oldExiled = player.Items?.FirstOrDefault(item => item?.Serial == oldSerial);
        if (oldExiled == null || SubModes.Count <= 1)
            return;

        int currentIndex = NormalizeIndex(state.CurrentMode);
        int nextIndex = (currentIndex + 1) % SubModes.Count;
        CustomItem current = SubModes[currentIndex].TargetItem;
        CustomItem next = SubModes[nextIndex].TargetItem;
        LabItem oldItem = LabItem.Get(oldExiled.Base);

        if (oldItem == null)
            return;

        try
        {
            state.SavedStates[currentIndex] = current.CallOnCaptureState(oldItem);
        }
        catch (Exception exception)
        {
            Log.Error($"[CustomHybrid] {GetType().Name} のモード状態保存に失敗しました: {exception}");
            return;
        }

        state.CurrentMode = nextIndex;
        pendingState = state;
        pendingModeIndex = nextIndex;

        ExiledItem newExiled = null;
        try
        {
            newExiled = player.AddItem(next.BaseType);
        }
        catch (Exception exception)
        {
            Log.Error($"[CustomHybrid] {GetType().Name} のモード切替中にアイテム追加が失敗しました: {exception}");
        }

        if (newExiled == null)
        {
            state.CurrentMode = currentIndex;
            pendingState = null;
            pendingModeIndex = 0;
            return;
        }

        LabItem newItem = LabItem.Get(newExiled.Base);
        if (newItem == null)
        {
            try
            {
                player.RemoveItem(newExiled, destroy: true);
            }
            catch (Exception exception)
            {
                Log.Warn($"[CustomHybrid] {GetType().Name} の未解決アイテム削除に失敗しました: {exception.Message}");
            }

            state.CurrentMode = currentIndex;
            pendingState = null;
            pendingModeIndex = 0;
            return;
        }

        bool rebound = false;
        bool nextModeWasStarted = startedModes.Contains(nextIndex);
        try
        {
            // Attach は同期的に OnTrackingStarted を呼ぶため、pending state を先に設定する。
            Rebind(newExiled.Serial);
            rebound = true;

            bool hasSavedState = state.SavedStates.TryGetValue(nextIndex, out object savedState);
            if (hasSavedState)
                next.CallCustomize(newItem);
            else
                next.CallCustomizeNewItem(newItem);

            switching = true;
            try
            {
                player.CurrentItem = newExiled;
            }
            finally
            {
                switching = false;
            }

            if (hasSavedState && savedState != null)
                next.CallOnRestoreState(newItem, savedState);

            next.CallOnModeActivated(newItem);
        }
        catch (Exception exception)
        {
            Log.Error($"[CustomHybrid] {GetType().Name} のモード切替に失敗しました: {exception}");
            RollbackModeSwitch(
                player,
                oldExiled,
                oldSerial,
                newExiled,
                state,
                currentIndex,
                nextIndex,
                rebound,
                nextModeWasStarted);
            return;
        }
        finally
        {
            pendingState = null;
            pendingModeIndex = 0;
        }

        states.Remove(oldSerial);
        try
        {
            player.RemoveItem(oldExiled, destroy: true);
        }
        catch (Exception exception)
        {
            Log.Error($"[CustomHybrid] {GetType().Name} の旧モード削除に失敗しました: {exception}");
        }

        if (!ShowModeSwitchHint)
            return;

        try
        {
            string modeName = string.IsNullOrWhiteSpace(SubModes[nextIndex].ModeName)
                ? next.Name
                : SubModes[nextIndex].ModeName;
            string modeDescription = string.IsNullOrWhiteSpace(SubModes[nextIndex].ModeDescription)
                ? next.Description
                : SubModes[nextIndex].ModeDescription;
            player.ShowHint(
                $"<size=24>モード切替: {modeName}</size>\n<size=23>{modeDescription}</size>",
                ModeSwitchHintDuration);
        }
        catch (Exception exception)
        {
            Log.Warn($"[CustomHybrid] {GetType().Name} のモード切替通知に失敗しました: {exception.Message}");
        }
    }

    private void RollbackModeSwitch(
        Player player,
        ExiledItem oldItem,
        ushort oldSerial,
        ExiledItem newItem,
        HybridState state,
        int currentIndex,
        int nextIndex,
        bool rebound,
        bool nextModeWasStarted)
    {
        state.CurrentMode = currentIndex;

        if (!nextModeWasStarted && startedModes.Remove(nextIndex))
        {
            try
            {
                // 新モードの context がまだ新 serial を指している間に片付ける。
                SubModes[nextIndex].TargetItem.CallOnTrackingStopped();
            }
            catch (Exception exception)
            {
                Log.Warn($"[CustomHybrid] {GetType().Name} の失敗モード後始末に失敗しました: {exception.Message}");
            }
        }

        if (rebound)
        {
            try
            {
                pendingState = state;
                pendingModeIndex = currentIndex;
                Rebind(oldSerial);
                states.Remove(newItem.Serial);

                switching = true;
                try
                {
                    player.CurrentItem = oldItem;
                }
                finally
                {
                    switching = false;
                }
            }
            catch (Exception exception)
            {
                Log.Error($"[CustomHybrid] {GetType().Name} のロールバック再紐付けに失敗しました: {exception}");
            }
        }

        try
        {
            player.RemoveItem(newItem, destroy: true);
        }
        catch (Exception exception)
        {
            Log.Warn($"[CustomHybrid] {GetType().Name} の失敗アイテム削除に失敗しました: {exception.Message}");
        }
    }

    /// <summary>ロードや外部 AddItem 後に、未登録 serial を初期モードへ紐付けます。</summary>
    public void RebindSerialFor(LabItem item, Player owner)
    {
        if (item == null || states.ContainsKey(item.Serial)) return;

        states[item.Serial] = new HybridState { CurrentMode = 0 };
        SetModeContext(item.Serial);
        Log.Debug($"[CustomHybrid] RebindSerialFor owner={owner?.Nickname} serial={item.Serial} modeIndex=0");
    }

    /// <summary>この serial が指定モードの現在の実体かどうかを返します。</summary>
    public bool IsCurrentSub(ushort serial, CustomItem sub)
        => ReferenceEquals(CurrentMode(serial)?.TargetItem, sub);

    /// <summary>デバッグ HUD 向けに現在モードと保存済みモードを返します。</summary>
    public string GetDebugStateFor(Player player, ushort serial)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<color=#88ffcc>[CustomHybrid]</color>");

        if (states.TryGetValue(serial, out HybridState state))
        {
            int index = NormalizeIndex(state.CurrentMode);
            string name = SubModes.Count == 0 ? "-" : GetModeName(index);
            string saved = state.SavedStates.Count == 0
                ? "-"
                : string.Join(",", state.SavedStates.Keys.OrderBy(key => key));
            builder.AppendLine($"  <color=#aaaaaa>Serial:</color> {serial}  <color=#aaaaaa>Mode:</color> {index} ({name})  <color=#aaaaaa>Saved:</color> {saved}");
        }
        else
        {
            builder.AppendLine($"  <color=#ff4444>Serial {serial} has no state.</color>");
        }

        return builder.ToString();
    }

    protected override void OnTrackingStarted()
    {
        if (!states.TryGetValue(Serial, out HybridState state))
        {
            state = pendingState ?? new HybridState { CurrentMode = NormalizeIndex(pendingModeIndex) };
            states[Serial] = state;
        }

        SetModeContext(Serial);
        int index = NormalizeIndex(state.CurrentMode);
        if (startedModes.Add(index))
        {
            try
            {
                SubModes[index].TargetItem.CallOnTrackingStarted();
            }
            catch
            {
                startedModes.Remove(index);
                throw;
            }
        }

        base.OnTrackingStarted();
    }

    protected override void OnTrackingStopped()
    {
        foreach (int index in startedModes.ToArray())
        {
            if (index >= 0 && index < SubModes.Count)
            {
                try
                {
                    SubModes[index].TargetItem.CallOnTrackingStopped();
                }
                catch (Exception exception)
                {
                    Log.Error($"[CustomHybrid] {GetType().Name} mode {index} の追跡終了処理に失敗しました: {exception}");
                }
            }
        }

        startedModes.Clear();
        states.Clear();
        SetModeContext(0);
        base.OnTrackingStopped();
    }

    protected override void OnPickupSpawned(LabPickup pickup)
    {
        EnsureState(pickup?.Serial ?? 0, 0);
        CurrentMode(pickup?.Serial ?? 0)?.TargetItem.CallOnPickupSpawned(pickup);
    }

    protected override void OnPickupDestroyed(PickupDestroyedEventArgs ev)
        => CurrentMode(ev?.Pickup?.Serial ?? 0)?.TargetItem.CallOnPickupDestroyed(ev);

    protected override void OnPickupStarting(PlayerPickingUpItemEventArgs ev)
        => CurrentMode(ev?.Pickup?.Serial ?? 0)?.TargetItem.CallOnPickupStarting(ev);

    protected override void OnArmorPickupStarting(PlayerPickingUpArmorEventArgs ev)
        => CurrentMode(ev?.BodyArmorPickup?.Serial ?? 0)?.TargetItem.CallOnArmorPickupStarting(ev);

    protected override void OnCandyPickupStarting(PlayerPickingUpScp330EventArgs ev)
        => CurrentMode(ev?.CandyPickup?.Serial ?? 0)?.TargetItem.CallOnCandyPickupStarting(ev);

    protected override void OnOwnerPickupStarting(PlayerPickingUpItemEventArgs ev)
        => CurrentMode(Serial)?.TargetItem.CallOnOwnerPickupStarting(ev);

    protected override void OnPickupCompleted(PlayerPickedUpItemEventArgs ev)
        => CurrentMode(ev?.Item?.Serial ?? 0)?.TargetItem.CallOnPickupCompleted(ev);

    protected override void OnArmorPickupCompleted(PlayerPickedUpArmorEventArgs ev)
        => CurrentMode(ev?.BodyArmorItem?.Serial ?? 0)?.TargetItem.CallOnArmorPickupCompleted(ev);

    protected override void OnCandyPickupCompleted(PlayerPickedUpScp330EventArgs ev)
        => CurrentMode(ev?.CandyItem?.Serial ?? 0)?.TargetItem.CallOnCandyPickupCompleted(ev);

    protected override void OnDropStarting(PlayerDroppingItemEventArgs ev)
        => CurrentMode(ev?.Item?.Serial ?? 0)?.TargetItem.CallOnDropStarting(ev);

    protected override void OnDropCompleted(PlayerDroppedItemEventArgs ev)
        => CurrentMode(ev?.Pickup?.Serial ?? 0)?.TargetItem.CallOnDropCompleted(ev);

    protected override void OnSelected(PlayerChangedItemEventArgs ev)
        => CurrentMode(ev?.NewItem?.Serial ?? 0)?.TargetItem.CallOnSelected(ev);

    protected override void OnDeselected(PlayerChangedItemEventArgs ev)
        => CurrentMode(ev?.OldItem?.Serial ?? 0)?.TargetItem.CallOnDeselected(ev);

    protected override void OnUseStarting(PlayerUsingItemEventArgs ev)
        => CurrentMode(ev?.UsableItem?.Serial ?? 0)?.TargetItem.CallOnUseStarting(ev);

    protected override void OnUseEffectsApplying(PlayerItemUsageEffectsApplyingEventArgs ev)
        => CurrentMode(ev?.UsableItem?.Serial ?? 0)?.TargetItem.CallOnUseEffectsApplying(ev);

    protected override void OnUseCompleted(PlayerUsedItemEventArgs ev)
        => CurrentMode(ev?.UsableItem?.Serial ?? 0)?.TargetItem.CallOnUseCompleted(ev);

    protected override void OnUseCancelling(PlayerCancellingUsingItemEventArgs ev)
        => CurrentMode(ev?.UsableItem?.Serial ?? 0)?.TargetItem.CallOnUseCancelling(ev);

    protected override void OnUseCancelled(PlayerCancelledUsingItemEventArgs ev)
        => CurrentMode(ev?.UsableItem?.Serial ?? 0)?.TargetItem.CallOnUseCancelled(ev);

    protected override void OnShotStarting(PlayerShootingWeaponEventArgs ev)
        => CurrentMode(ev?.FirearmItem?.Serial ?? 0)?.TargetItem.CallOnShotStarting(ev);

    protected override void OnShotCompleted(PlayerShotWeaponEventArgs ev)
        => CurrentMode(ev?.FirearmItem?.Serial ?? 0)?.TargetItem.CallOnShotCompleted(ev);

    protected override void OnDryFireStarting(PlayerDryFiringWeaponEventArgs ev)
        => CurrentMode(ev?.FirearmItem?.Serial ?? 0)?.TargetItem.CallOnDryFireStarting(ev);

    protected override void OnDryFireCompleted(PlayerDryFiredWeaponEventArgs ev)
        => CurrentMode(ev?.FirearmItem?.Serial ?? 0)?.TargetItem.CallOnDryFireCompleted(ev);

    protected override void OnReloadStarting(PlayerReloadingWeaponEventArgs ev)
        => CurrentMode(ev?.FirearmItem?.Serial ?? 0)?.TargetItem.CallOnReloadStarting(ev);

    protected override void OnReloadCompleted(PlayerReloadedWeaponEventArgs ev)
        => CurrentMode(ev?.FirearmItem?.Serial ?? 0)?.TargetItem.CallOnReloadCompleted(ev);

    protected override void OnAttachmentsChanging(PlayerChangingAttachmentsEventArgs ev)
        => CurrentMode(ev?.FirearmItem?.Serial ?? 0)?.TargetItem.CallOnAttachmentsChanging(ev);

    protected override void OnAttachmentsChanged(PlayerChangedAttachmentsEventArgs ev)
        => CurrentMode(ev?.FirearmItem?.Serial ?? 0)?.TargetItem.CallOnAttachmentsChanged(ev);

    protected override void OnHurtingPlayer(PlayerHurtingEventArgs ev)
        => CurrentMode(ev?.Attacker?.CurrentItem?.Serial ?? 0)?.TargetItem.CallOnHurtingPlayer(ev);

    protected override void OnOwnerHurting(PlayerHurtingEventArgs ev)
        => CurrentMode(Serial)?.TargetItem.CallOnOwnerHurting(ev);

    protected override void OnOwnerDying(PlayerDyingEventArgs ev)
        => CurrentMode(Serial)?.TargetItem.CallOnOwnerDying(ev);

    protected override void OnProjectileThrowStarting(PlayerThrowingProjectileEventArgs ev)
        => CurrentMode(ev?.ThrowableItem?.Serial ?? 0)?.TargetItem.CallOnProjectileThrowStarting(ev);

    protected override void OnProjectileThrown(PlayerThrewProjectileEventArgs ev)
        => CurrentMode(ev?.ThrowableItem?.Serial ?? 0)?.TargetItem.CallOnProjectileThrown(ev);

    protected override void OnKeycardInspectionStarting(PlayerInspectingKeycardEventArgs ev)
        => CurrentMode(ev?.KeycardItem?.Serial ?? 0)?.TargetItem.CallOnKeycardInspectionStarting(ev);

    protected override void OnKeycardInspected(PlayerInspectedKeycardEventArgs ev)
        => CurrentMode(ev?.KeycardItem?.Serial ?? 0)?.TargetItem.CallOnKeycardInspected(ev);

    protected override void OnScp914ProcessingPickup(Scp914ProcessingPickupEventArgs ev)
        => CurrentMode(ev?.Pickup?.Serial ?? 0)?.TargetItem.CallOnScp914ProcessingPickup(ev);

    protected override void OnScp914ProcessingInventoryItem(Scp914ProcessingInventoryItemEventArgs ev)
        => CurrentMode(ev?.Item?.Serial ?? 0)?.TargetItem.CallOnScp914ProcessingInventoryItem(ev);

    protected override void Customize(LabItem item)
        => CurrentMode(item?.Serial ?? 0)?.TargetItem.CallCustomize(item);

    protected override void CustomizeNewItem(LabItem item)
        => CurrentMode(item?.Serial ?? 0)?.TargetItem.CallCustomizeNewItem(item);

    protected override void Customize(LabPickup pickup)
        => CurrentMode(pickup?.Serial ?? 0)?.TargetItem.CallCustomize(pickup);

    protected override LabPickup CreatePickup(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        CustomItem mode = CurrentMode()?.TargetItem;
        if (mode == null && SubModes.Count > 0)
            mode = SubModes[NormalizeIndex(pendingModeIndex)].TargetItem;

        return mode?.CallCreatePickup(position, rotation, scale)
               ?? base.CreatePickup(position, rotation, scale);
    }

    private CustomHybridMode CurrentMode() => CurrentMode(Serial);

    private List<CustomHybridMode> CreateSubModes()
    {
        List<CustomHybridMode> modes = BuildSubModes();
        if (modes == null || modes.Count == 0)
            throw new InvalidOperationException($"{GetType().Name} must define at least one CustomHybrid mode.");

        if (modes.Any(mode => mode?.TargetItem == null))
            throw new InvalidOperationException($"{GetType().Name} contains an invalid CustomHybrid mode.");

        return modes;
    }

    private CustomHybridMode CurrentMode(ushort serial)
    {
        if (!states.TryGetValue(serial, out HybridState state) || SubModes.Count == 0)
            return null;

        int index = NormalizeIndex(state.CurrentMode);
        return index < SubModes.Count ? SubModes[index] : null;
    }

    private int NormalizeIndex(int index)
        => SubModes.Count == 0 ? 0 : (index < 0 || index >= SubModes.Count ? 0 : index);

    private string GetModeName(int index)
    {
        if (index < 0 || index >= SubModes.Count)
            return $"Mode#{index}";

        CustomHybridMode mode = SubModes[index];
        return string.IsNullOrWhiteSpace(mode.ModeName) ? mode.TargetItem.Name : mode.ModeName;
    }

    private void EnsureState(ushort serial, int modeIndex)
    {
        if (serial == 0 || states.ContainsKey(serial)) return;

        states[serial] = new HybridState { CurrentMode = NormalizeIndex(modeIndex) };
        SetModeContext(serial);
    }

    private void SetModeContext(ushort serial)
    {
        foreach (CustomHybridMode mode in SubModes)
            mode?.TargetItem?.SetHybridContext(
                serial,
                serial == 0 ? null : Destroy,
                serial == 0 ? null : RefreshPickupSchematic);
    }
}

/// <summary>CustomHybrid の 1 モード定義です。</summary>
public class CustomHybridMode
{
    private CustomItem targetItem;

    public CustomHybridMode(CustomItem targetItem, string modeName = "", string modeDescription = "")
    {
        TargetItem = targetItem;
        ModeName = modeName ?? string.Empty;
        ModeDescription = modeDescription ?? string.Empty;
    }

    public CustomItem TargetItem
    {
        get => targetItem;
        set => targetItem = value ?? throw new ArgumentNullException(nameof(value));
    }
    public string ModeName { get; set; }
    public string ModeDescription { get; set; }
}
