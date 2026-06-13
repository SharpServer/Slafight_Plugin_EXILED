using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;
using ExiledRadio = Exiled.API.Features.Items.Radio;
using ExiledScp1344 = Exiled.API.Features.Items.Scp1344;
using Item = Exiled.API.Features.Items.Item;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public abstract class ScpcbBatteryBase : CItemUsable
{
    private const float HudInterval = 1f;
    private const float HudDisplaySeconds = 1.25f;
    private const float ResultDisplaySeconds = 2.5f;

    private readonly Dictionary<int, int> _selectedTargetIndexes = new();
    private readonly Dictionary<int, CoroutineHandle> _hudLoops = new();
    private readonly Dictionary<int, float> _keepInfoUntil = new();

    protected override ItemType BaseItem => ItemType.Medkit;
    protected override int MaxUseCount => 1;
    protected override bool PickupLightEnabled => true;

    protected virtual BatteryBehavior Behavior => BatteryBehavior.Recharge;
    protected virtual float RechargeAmount => 100f;
    protected virtual bool FullRecharge => true;
    protected virtual string ChargeLabel => FullRecharge ? "100%" : $"+{RechargeAmount:0}%";
    protected virtual string InertText => "この電池は電圧が合わず、NVG / S-Nav / Radio には使えません。";
    protected virtual string LethalText => "異常な電池が強烈な電流を放っています。";

    protected enum BatteryBehavior
    {
        Recharge,
        Inert,
        Lethal,
    }

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Player.Left += OnLeft;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Player.Left -= OnLeft;
        ClearRuntimeState();
        base.UnregisterEvents();
    }

    protected override void OnWaitingForPlayers()
    {
        ClearRuntimeState();
        base.OnWaitingForPlayers();
    }

    protected override void OnAcquired(ItemAddedEventArgs ev, bool displayMessage)
    {
        base.OnAcquired(ev, displayMessage);
        if (ev.Player != null)
            _selectedTargetIndexes.TryAdd(ev.Player.Id, 0);

        if (ev.Player != null && Behavior == BatteryBehavior.Lethal)
            ShockPlayer(ev.Player);
    }

    protected override void OnChangingItem(ChangingItemEventArgs ev)
    {
        base.OnChangingItem(ev);

        if (ev.Player == null || ev.Item == null || !Check(ev.Item))
            return;

        StartHudLoop(ev.Player);
        UpdateHud(ev.Player, ev.Item);
    }

    protected override void OnDropping(DroppingItemEventArgs ev)
    {
        if (ev.Player == null || ev.Item == null || !Check(ev.Item))
            return;

        if (Behavior != BatteryBehavior.Recharge)
            return;
        
        if (!ev.IsThrown)
            return;

        ev.IsAllowed = false;
        CycleTarget(ev.Player, ev.Item);
    }

    protected override bool CanStartUse(UsingItemEventArgs ev)
    {
        if (ev.Player == null || ev.Item == null)
            return false;

        return Behavior switch
        {
            BatteryBehavior.Recharge => GetSelectedTarget(ev.Player, ev.Item)?.CanRecharge == true,
            BatteryBehavior.Lethal => true,
            _ => false,
        };
    }

    protected override void OnStartUseDenied(UsingItemEventArgs ev)
    {
        if (ev.Player == null || ev.Item == null) return;
        ShowTemporary(ev.Player, BuildDeniedText(ev.Player, ev.Item));
    }

    protected override bool CanUse(UsingItemCompletedEventArgs ev)
    {
        if (ev.Player == null || ev.Item == null)
            return false;

        return Behavior switch
        {
            BatteryBehavior.Recharge => GetSelectedTarget(ev.Player, ev.Item)?.CanRecharge == true,
            BatteryBehavior.Lethal => true,
            _ => false,
        };
    }

    protected override void OnUseDenied(UsingItemCompletedEventArgs ev)
    {
        if (ev.Player == null || ev.Item == null) return;
        ShowTemporary(ev.Player, BuildDeniedText(ev.Player, ev.Item));
    }

    protected override void OnUsedEffect(UsingItemCompletedEventArgs ev)
    {
        if (ev.Player == null || ev.Item == null) return;

        if (Behavior == BatteryBehavior.Lethal)
        {
            ShockPlayer(ev.Player);
            return;
        }

        var target = GetSelectedTarget(ev.Player, ev.Item);
        if (target == null || !target.CanRecharge)
            return;

        var before = target.Percent;
        target.Recharge(RechargeAmount, FullRecharge);
        var after = target.Percent;

        ShowTemporary(
            ev.Player,
            $"<color=#88ff88>{target.DisplayName}</color> を充電しました: {before:0}% -> {after:0}%");
    }

    private void OnLeft(LeftEventArgs ev)
    {
        if (ev.Player == null) return;

        _selectedTargetIndexes.Remove(ev.Player.Id);
        _keepInfoUntil.Remove(ev.Player.Id);

        if (_hudLoops.Remove(ev.Player.Id, out var handle) && handle.IsRunning)
            Timing.KillCoroutines(handle);
    }

    private void CycleTarget(Player player, Item sourceItem)
    {
        var targets = BatteryRechargeTargets.Find(player, sourceItem.Serial);
        if (targets.Count == 0)
        {
            _selectedTargetIndexes[player.Id] = 0;
            UpdateHud(player, sourceItem);
            return;
        }

        var current = _selectedTargetIndexes.GetValueOrDefault(player.Id);
        _selectedTargetIndexes[player.Id] = (current + 1) % targets.Count;
        _keepInfoUntil.Remove(player.Id);
        UpdateHud(player, sourceItem, force: true);
    }

    private IRechargeableBatteryTarget? GetSelectedTarget(Player player, Item sourceItem)
    {
        var targets = BatteryRechargeTargets.Find(player, sourceItem.Serial);
        if (targets.Count == 0)
            return null;

        var selected = Mathf.Clamp(_selectedTargetIndexes.GetValueOrDefault(player.Id), 0, targets.Count - 1);
        _selectedTargetIndexes[player.Id] = selected;
        return targets[selected];
    }

    private void StartHudLoop(Player player)
    {
        if (_hudLoops.TryGetValue(player.Id, out var existing) && existing.IsRunning)
            return;

        _hudLoops[player.Id] = Timing.RunCoroutine(HudLoop(player.Id));
    }

    private IEnumerator<float> HudLoop(int playerId)
    {
        while (true)
        {
            yield return Timing.WaitForSeconds(HudInterval);

            var player = Player.Get(playerId);
            if (player == null || !player.IsConnected)
                break;

            var held = player.CurrentItem;
            if (held == null || !Check(held))
            {
                ClearHudIfExpired(player);
                break;
            }

            UpdateHud(player, held);
        }

        _hudLoops.Remove(playerId);
    }

    private void UpdateHud(Player player, Item sourceItem, bool force = false)
    {
        if (!force && IsTemporaryInfoActive(player))
            return;

        player.ShowHint(BuildHudText(player, sourceItem), HudDisplaySeconds);
    }

    private string BuildHudText(Player player, Item sourceItem)
    {
        if (Behavior == BatteryBehavior.Inert)
            return $"<color=#ffd966>{DisplayName}</color>\n<color=#aaaaaa>{InertText}</color>";

        if (Behavior == BatteryBehavior.Lethal)
            return $"<color=#ff5555>{DisplayName}</color>\n<color=#ff7777>{LethalText}</color>";

        var targets = BatteryRechargeTargets.Find(player, sourceItem.Serial);
        if (targets.Count == 0)
            return $"<color=#ffd966>{DisplayName}</color>\n<color=#ff7777>充電可能な対象がありません</color>";

        var target = GetSelectedTarget(player, sourceItem)!;
        var stateColor = target.CanRecharge ? "#88ff88" : "#aaaaaa";
        var actionText = target.CanRecharge
            ? $"充電量: <color=#88ff88>{ChargeLabel}</color>"
            : "<color=#aaaaaa>満充電</color>";

        return
            $"<color=#ffd966>{DisplayName}</color>\n" +
            $"対象: <color=#88ccff>{target.DisplayName}</color> <color=#aaaaaa>({target.Kind})</color>\n" +
            $"電池: <color={stateColor}>{target.Percent:0}%</color>\n" +
            $"{actionText}\n" +
            "<color=#888888>ドロップ: 対象切替 / 使用: 充電</color>";
    }

    private string BuildDeniedText(Player player, Item sourceItem)
    {
        if (Behavior == BatteryBehavior.Inert)
            return $"<color=#ff7777>{InertText}</color>";

        if (Behavior == BatteryBehavior.Lethal)
            return $"<color=#ff7777>{LethalText}</color>";

        var target = GetSelectedTarget(player, sourceItem);
        return target == null
            ? "<color=#ff7777>充電可能な対象がありません</color>"
            : $"<color=#ff7777>{target.DisplayName} は既に満充電です</color>";
    }

    private void ShowTemporary(Player player, string text)
    {
        _keepInfoUntil[player.Id] = Time.time + ResultDisplaySeconds;
        player.ShowHint(text, ResultDisplaySeconds);
    }

    private void ClearHudIfExpired(Player player)
    {
        if (IsTemporaryInfoActive(player))
            return;

        _keepInfoUntil.Remove(player.Id);
    }

    private bool IsTemporaryInfoActive(Player player)
    {
        if (!_keepInfoUntil.TryGetValue(player.Id, out var keepUntil))
            return false;

        if (Time.time < keepUntil)
            return true;

        _keepInfoUntil.Remove(player.Id);
        return false;
    }

    private void ClearRuntimeState()
    {
        foreach (var handle in _hudLoops.Values.ToList())
        {
            if (handle.IsRunning)
                Timing.KillCoroutines(handle);
        }

        _hudLoops.Clear();
        _selectedTargetIndexes.Clear();
        _keepInfoUntil.Clear();
    }

    private void ShockPlayer(Player player)
    {
        if (player == null || player.ReferenceHub == null || !player.IsAlive)
            return;

        player.ShowHint($"<color=#ff5555>{DisplayName}</color>\n<color=#ff7777>{LethalText}</color>", 2.5f);
        Timing.CallDelayed(0.1f, () =>
        {
            if (player.ReferenceHub != null && player.IsAlive)
            {
                SpeakerApi.Play("PowerUp.ogg", "BatteryStrange", player.Position, isSpatial: true, maxDistance: 8.5f, minDistance:7.5f);
                player.ExplodeEffect(ProjectileType.FragGrenade);
                player.Vaporize();
                player.ShowHint("<color=red>うわあああああああああ...!!!\n<size=24>あなたは奇妙なバッテリーで感電死した!!!!!</size></color>", 5.5f);
            }
        });
    }
}

public sealed class ScpcbBattery9V : ScpcbBatteryBase
{
    public override string DisplayName => "9V Battery";
    public override string Description =>
        "小型の角形電池。ドロップで充電対象を選び、使用すると対象のバッテリーを満充電にする。";

    protected override string UniqueKey => "ScpcbBattery9V";
    protected override string? PickupSchematicName => "Battery9V";
    protected override Color PickupLightColor => Color.yellow;
}

public sealed class ScpcbBattery18V : ScpcbBatteryBase
{
    public override string DisplayName => "18V Battery";
    public override string Description =>
        "高出力の角形電池。NVG、S-Nav、Radioには電圧が合わず、充電には使えない。";

    protected override string UniqueKey => "ScpcbBattery18V";
    protected override BatteryBehavior Behavior => BatteryBehavior.Inert;
    protected override string? PickupSchematicName => "Battery18V";
    protected override Color PickupLightColor => new(1f, 0.45f, 0f);
}

public sealed class ScpcbBatteryStrange : ScpcbBatteryBase
{
    public override string DisplayName => "Strange Battery";
    public override string Description =>
        "異常な電池。触れるだけで危険な電流を放つ。";

    protected override string UniqueKey => "ScpcbBatteryStrange";
    protected override BatteryBehavior Behavior => BatteryBehavior.Lethal;
    protected override string? PickupSchematicName => "BatteryStrange";
    protected override bool PickupLightEnabled => false;
}

public interface IRechargeableBatteryTarget
{
    string Kind { get; }
    string DisplayName { get; }
    float Percent { get; }
    bool CanRecharge { get; }
    void Recharge(float amount, bool fullRecharge);
}

public static class BatteryRechargeTargets
{
    private static readonly IReadOnlyList<Func<Player, Item, IRechargeableBatteryTarget?>> Providers =
    [
        TryCreateNvgTarget,
        TryCreateSnavTarget,
        TryCreateRadioTarget,
    ];

    public static List<IRechargeableBatteryTarget> Find(Player player, ushort sourceSerial)
    {
        var targets = new List<IRechargeableBatteryTarget>();

        foreach (var item in player.Items.ToList())
        {
            if (item == null || item.Serial == sourceSerial)
                continue;

            foreach (var provider in Providers)
            {
                var target = provider(player, item);
                if (target == null)
                    continue;

                targets.Add(target);
                break;
            }
        }

        return targets;
    }

    private static IRechargeableBatteryTarget? TryCreateNvgTarget(Player player, Item item)
    {
        if (item is not ExiledScp1344 scp1344)
            return null;

        if (!CItem.TryGet(item, out var cItem) || cItem is not CItemNvg)
            return null;

        return new NvgBatteryTarget(scp1344, cItem.DisplayName);
    }

    private static IRechargeableBatteryTarget? TryCreateSnavTarget(Player player, Item item)
    {
        if (item is not ExiledRadio radio)
            return null;

        if (!CItem.TryGet(item, out var cItem) || cItem is not SNAV300)
            return null;

        return new RadioBatteryTarget(radio, cItem.DisplayName, "S-Nav");
    }

    private static IRechargeableBatteryTarget? TryCreateRadioTarget(Player player, Item item)
    {
        if (item is not ExiledRadio radio)
            return null;

        if (CItem.TryGet(item.Serial, out _))
            return null;

        return new RadioBatteryTarget(radio, "Radio", "Radio");
    }

    private sealed class NvgBatteryTarget : IRechargeableBatteryTarget
    {
        private readonly ExiledScp1344 _item;

        public NvgBatteryTarget(ExiledScp1344 item, string displayName)
        {
            _item = item;
            DisplayName = displayName;
        }

        public string Kind => "NVG";
        public string DisplayName { get; }
        public float Percent => CItemNvg.GetBattery(_item.Serial, 100f);
        public bool CanRecharge => Percent < 100f;

        public void Recharge(float amount, bool fullRecharge)
        {
            var next = fullRecharge ? 100f : Percent + amount;
            CItemNvg.SetBattery(_item.Serial, next, reviveIfDead: true);
        }
    }

    private sealed class RadioBatteryTarget : IRechargeableBatteryTarget
    {
        private readonly ExiledRadio _item;

        public RadioBatteryTarget(ExiledRadio item, string displayName, string kind)
        {
            _item = item;
            DisplayName = displayName;
            Kind = kind;
        }

        public string Kind { get; }
        public string DisplayName { get; }
        public float Percent => _item.BatteryLevel;
        public bool CanRecharge => _item.BatteryLevel < 100;

        public void Recharge(float amount, bool fullRecharge)
        {
            var next = fullRecharge ? 100 : Mathf.Clamp(Mathf.RoundToInt(_item.BatteryLevel + amount), 0, 100);
            _item.BatteryLevel = (byte)next;
        }
    }
}
