using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using Exiled.Events.EventArgs.Player;
using MEC;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class Toolbox : CItem
{
    public override string DisplayName => "Toolbox";
    public override string Description => "様々な作業を行うことができる便利な工具箱。\nTキーで使う機能を切り替えられる。";
    protected override string UniqueKey => "Toolbox";
    protected override ItemType BaseItem => ItemType.Coin;
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.yellow;
    protected override string PickupSchematicName => "ToolboxModel";
    protected override bool UseSchematicPickup => true;

    public enum UtilType
    {
        Work,MaintenanceLock
    }
    public struct ToolboxStats
    {
        public UtilType SelectedUtilType;
        public float AwaitingCooldownDuration;
        public bool IsAwaitingCooldown => AwaitingCooldownDuration > 0;
    }
    private static readonly Dictionary<Player, ToolboxStats> ToolboxStatsMap = [];
    private static readonly Dictionary<Player, CoroutineHandle> HintLoopHandles = [];

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Player.Left += OnPlayerLeft;
        Exiled.Events.Handlers.Player.InteractingDoor += OnInteractingDoor;
        Exiled.Events.Handlers.Player.UnlockingGenerator += OnInteractingGenerator;
        Exiled.Events.Handlers.Player.FlippingCoin += OnCoin;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Player.Left -= OnPlayerLeft;
        Exiled.Events.Handlers.Player.InteractingDoor -= OnInteractingDoor;
        Exiled.Events.Handlers.Player.UnlockingGenerator -= OnInteractingGenerator;
        Exiled.Events.Handlers.Player.FlippingCoin -= OnCoin;
        base.UnregisterEvents();
    }

    protected override void OnWaitingForPlayers()
    {
        foreach (var h in HintLoopHandles.Values) Timing.KillCoroutines(h);
        HintLoopHandles.Clear();
        ToolboxStatsMap.Clear();
    }

    private static void OnPlayerLeft(LeftEventArgs ev)
    {
        if (ev.Player == null) return;
        if (HintLoopHandles.TryGetValue(ev.Player, out var h)) Timing.KillCoroutines(h);
        HintLoopHandles.Remove(ev.Player);
        ToolboxStatsMap.Remove(ev.Player);
    }

    protected override void OnAcquired(ItemAddedEventArgs ev, bool displayMessage)
    {
        ToolboxStatsMap.TryAdd(ev.Player, new ToolboxStats { SelectedUtilType = UtilType.Work, AwaitingCooldownDuration = 0f });
        base.OnAcquired(ev, displayMessage);
    }

    protected override void OnSelectedHintFinished(Player player)
    {
        // PickedUp / Selected の両 Hint が流れ終わったあとにループ起動。
        // (PickedUp は 4s、Selected は 3s で、Selected は Pickup 中に上書きされるため、
        //  Selected 終了 = 両方流れ切ったタイミングとみなして良い)
        if (!CheckHeld(player)) return;
        ToolboxStatsMap.TryAdd(player, new ToolboxStats { SelectedUtilType = UtilType.Work, AwaitingCooldownDuration = 0f });

        if (HintLoopHandles.TryGetValue(player, out var running) && running.IsRunning) return;
        HintLoopHandles[player] = Timing.RunCoroutine(HintLoopCoroutine(player));
    }

    protected override void OnDropping(DroppingItemEventArgs ev)
    {
        if (!ev.IsThrown) return;
        ev.IsAllowed = false;
        if (!ToolboxStatsMap.TryGetValue(ev.Player, out var stats)) return;

        stats.SelectedUtilType = stats.SelectedUtilType switch
        {
            UtilType.Work => UtilType.MaintenanceLock,
            UtilType.MaintenanceLock => UtilType.Work,
            _ => throw new ArgumentOutOfRangeException()
        };
        ToolboxStatsMap[ev.Player] = stats;
        // Hint はループ側が次 tick で反映
    }

    private void OnInteractingDoor(InteractingDoorEventArgs ev)
    {
        // Log.Debug($"{ev.Player}: Interacting Door\nCheckHold: {CheckHeld(ev.Player)}, ToolboxStstsTryGet: {ToolboxStatsMap.TryGetValue(ev.Player, out _)}");
        if (!CheckHeld(ev.Player)) return;
        if (!ToolboxStatsMap.TryGetValue(ev.Player, out var stats)) return;
        if (stats.IsAwaitingCooldown) return;
        if (stats.SelectedUtilType is UtilType.Work)
        {
            if (ev.Door is not BreakableDoor breakableDoor) return;
            if (breakableDoor.IsDestroyed) return;
            breakableDoor.Break();
            Timing.RunCoroutine(CooldownCoroutine(ev.Player));
        }
        else
        {
            if (ev.Door.IsLocked) return;
            ev.Door.IsOpen = false;
            Timing.CallDelayed(0.5f, () =>
            {
                ev.Door.Lock(30f, DoorLockType.Lockdown079);
            });
            Timing.RunCoroutine(CooldownCoroutine(ev.Player));
        }
    }

    private void OnCoin(FlippingCoinEventArgs ev)
    {
        if (!CheckHeld(ev.Player)) return;
        if (!ToolboxStatsMap.TryGetValue(ev.Player, out var stats)) return;
        if (stats.IsAwaitingCooldown) return;
        if (stats.SelectedUtilType is UtilType.Work)
        {
            var list = Door.List.Where(d => Vector3.Distance(d.Position, ev.Player.Position) <= 3f).ToList();
            if (list.Count >= 1)
            {
                var door = list.First();
                if (door is not BreakableDoor breakableDoor) return;
                if (!breakableDoor.IsDestroyed) return;
                breakableDoor.Repair();
                Timing.RunCoroutine(CooldownCoroutine(ev.Player));
            }
        }
    }

    private void OnInteractingGenerator(UnlockingGeneratorEventArgs ev)
    {
        if (!CheckHeld(ev.Player)) return;
        if (!ToolboxStatsMap.TryGetValue(ev.Player, out var stats)) return;
        if (stats.SelectedUtilType is not UtilType.Work) return;
        if (stats.IsAwaitingCooldown) return;

        // Engineer.cs と同じ「強制解錠」パターン
        ev.IsAllowed = true;
        ev.Generator.State = GeneratorState.Unlocked;
        ev.Generator.IsUnlocked = true;

        Timing.RunCoroutine(CooldownCoroutine(ev.Player));
    }

    private IEnumerator<float> HintLoopCoroutine(Player player)
    {
        while (true)
        {
            if (Round.IsLobby) yield break;
            if (!CheckHeld(player)) yield break;
            if (!ToolboxStatsMap.TryGetValue(player, out var stats)) yield break;

            player.ShowHint(
                $"<size=24>現在選択されている機能：{GetTranslatedText(stats.SelectedUtilType)}</size>\n" +
                $"<size=22>{GetTranslatedText(stats.SelectedUtilType, true)}\n{TryGetExternalCooldownText(player)}</size>",
                1.2f);

            yield return Timing.WaitForSeconds(1f);
        }
    }

    private static IEnumerator<float> CooldownCoroutine(Player player, float cooldownTime = 60f)
    {
        if (!ToolboxStatsMap.TryGetValue(player, out var stats)) yield break;

        // 開始時にクールダウン時間を実際に載せる (旧実装ではここが抜けてて絶対 0 のまま抜けていた)
        stats.AwaitingCooldownDuration = cooldownTime;
        ToolboxStatsMap[player] = stats;

        while (true)
        {
            if (Round.IsLobby || ToolboxStatsMap.IsEmpty()) yield break;
            if (!ToolboxStatsMap.TryGetValue(player, out stats)) yield break;
            if (stats.AwaitingCooldownDuration <= 0f) yield break;

            stats.AwaitingCooldownDuration -= 1f;
            ToolboxStatsMap[player] = stats;
            yield return Timing.WaitForSeconds(1f);
        }
    }

    private static string GetTranslatedText(UtilType utilType, bool isDesc = false)
    {
        string result;
        if (!isDesc)
        {
            result = utilType switch
            {
                UtilType.Work => "作業",
                UtilType.MaintenanceLock => "メンテナンスロック",
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        else
        {
            result = utilType switch
            {
                UtilType.Work => "ドアに向かってインタラクトすることで破壊、近くでコイン使用で修理出来る。\n発電機に対してインタラクトした場合は強制的に開けることができる。",
                UtilType.MaintenanceLock => "ドアに向かってインタラクトすることでドアをメンテナンスモードにでき、一定時間閉じた状態でロックできる。",
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        return result;
    }

    private static string TryGetExternalCooldownText(Player player)
    {
        if (!ToolboxStatsMap.TryGetValue(player, out var stats)) return string.Empty;
        return stats.IsAwaitingCooldown ? $"(クールダウン中：{stats.AwaitingCooldownDuration}秒)" : string.Empty;
    }
}
