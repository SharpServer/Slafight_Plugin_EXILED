using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using Exiled.API.Features.Pickups;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.MainHandlers;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Slafight_Plugin_EXILED.SpecialEvents.Events;

public class Scp1509BattleFieldEvent : SpecialEvent
{
    // ===== メタ情報 =====
    public override SpecialEventType EventType => SpecialEventType.Scp1509BattleField;
    public override int MinPlayersRequired => 4;
    public override string LocalizedName => "Scp1509BattleField";
    public override string TriggerRequirement => "4人以上のプレイヤー";

    // ===== 内部状態 =====
    private int _eventPid;
    // ===== 実行エントリポイント =====
    protected override void OnExecute(int eventPID)
    {
        _eventPid = eventPID;

        if (CancelIfOutdated())
            return;

        RunBattleField();
    }

    public override void RegisterEvents() { }
    public override void UnregisterEvents() { }

    private new bool CancelIfOutdated()
        => _eventPid != SpecialEventsHandler.Instance.EventPID;

    // ===== メイン処理 =====
    private void RunBattleField()
    {
        RoundHazardController.DisableLightDecontamination();

        if (CancelIfOutdated())
            return;

        SpawnSystem.Disable = true;

        // 回復系以外のピックアップ削除
        List<ItemType> keepPickups =
        [
            ItemType.Painkillers,
            ItemType.Medkit,
            ItemType.Adrenaline
        ];

        foreach (Pickup pickup in Pickup.List)
        {
            if (!keepPickups.Contains(pickup.Type))
                pickup.Destroy();
        }

        if (CancelIfOutdated())
            return;

        // マップ選択
        int battlefield_map = Random.Range(1, 5);

        // プレイヤーを2チームに分けて1509装備配布
        Timing.CallDelayed(1.11f, () =>
        {
            if (CancelIfOutdated()) return;

            int i = 0;
            bool firstPattern = Random.Range(1, 3) == 1;

            foreach (Player player in Player.List)
            {
                bool chaos = firstPattern ? (i % 2 == 0) : (i % 2 == 1);

                if (chaos)
                    player.Role.Set(RoleTypeId.ChaosRifleman);
                else
                    player.Role.Set(RoleTypeId.NtfPrivate);

                player.MaxHealth = 300f;
                player.Health = player.MaxHealth;
                player.ClearInventory();
                player.AddItem(ItemType.SCP1509);
                player.AddItem(ItemType.Medkit);
                player.AddItem(ItemType.Medkit);
                player.AddItem(ItemType.Adrenaline);
                player.AddItem(ItemType.SCP500);
                player.AddItem(ItemType.SCP500);
                player.AddItem(ItemType.ArmorCombat);

                i++;
            }
        });

        // マップ設定とスポーン位置調整
        Timing.CallDelayed(2.0f, () =>
        {
            if (CancelIfOutdated()) return;

            if (battlefield_map == 1)
            {
                // Surface: 全ドア開放＆ロック
                foreach (Door door in Door.List)
                {
                    door.IsOpen = true;
                    door.Lock(DoorLockType.AdminCommand);
                }
            }
            else if (battlefield_map == 2)
            {
                // Entrance
                List<DoorType> closeDoor = [DoorType.CheckpointGateA, DoorType.CheckpointGateB];
                foreach (Door door in Door.List)
                {
                    if (closeDoor.Contains(door.Type))
                    {
                        door.IsOpen = false;
                    }
                    else
                    {
                        door.IsOpen = true;
                    }
                    door.Lock(DoorLockType.AdminCommand);
                }

                // チーム別スポーン
                foreach (Player player in Player.List)
                {
                    if (player.Role == RoleTypeId.ChaosRifleman)
                    {
                        Vector3 chaosspawn = Door.Get(DoorType.GateA).Position;
                        player.Position = chaosspawn + new Vector3(0f, 2f, 0f);
                    }
                    else if (player.Role == RoleTypeId.NtfPrivate)
                    {
                        Vector3 ntfspawn = Door.Get(DoorType.GateB).Position;
                        player.Position = ntfspawn + new Vector3(0f, 2f, 0f);
                    }
                }
            }
            else if (battlefield_map == 3)
            {
                // Heavy Containment
                List<DoorType> closeDoor = [DoorType.CheckpointGateA, DoorType.CheckpointGateB];
                foreach (Door door in Door.List)
                {
                    if (closeDoor.Contains(door.Type))
                    {
                        door.IsOpen = false;
                    }
                    else
                    {
                        door.IsOpen = true;
                    }
                    door.Lock(DoorLockType.AdminCommand);
                }

                foreach (Player player in Player.List)
                {
                    if (player.Role == RoleTypeId.ChaosRifleman)
                    {
                        Vector3 chaosspawn = Door.Get(DoorType.CheckpointEzHczA).Position;
                        player.Position = chaosspawn + new Vector3(0f, 2f, 0f);
                    }
                    else if (player.Role == RoleTypeId.NtfPrivate)
                    {
                        Vector3 ntfspawn = Door.Get(DoorType.CheckpointEzHczB).Position;
                        player.Position = ntfspawn + new Vector3(0f, 2f, 0f);
                    }
                }
            }
            else if (battlefield_map == 4)
            {
                // Light Containment
                List<DoorType> closeDoor = [DoorType.Scp914Door, DoorType.Scp914Gate];
                foreach (Door door in Door.List)
                {
                    if (closeDoor.Contains(door.Type))
                    {
                        door.IsOpen = false;
                    }
                    else
                    {
                        door.IsOpen = true;
                    }
                    door.Lock(DoorLockType.AdminCommand);
                }

                foreach (Player player in Player.List)
                {
                    if (player.Role == RoleTypeId.ChaosRifleman)
                    {
                        Vector3 chaosspawn = Door.Get(DoorType.CheckpointLczA).Position;
                        player.Position = chaosspawn + new Vector3(0f, 2f, 0f);
                    }
                    else if (player.Role == RoleTypeId.NtfPrivate)
                    {
                        Vector3 ntfspawn = Door.Get(DoorType.CheckpointLczB).Position;
                        player.Position = ntfspawn + new Vector3(0f, 2f, 0f);
                    }
                }
            }
        });
        
        // メインエレベーター停止（戦場閉じ込め）
        List<ElevatorType> lockEvTypes =
        [
            ElevatorType.GateA,
            ElevatorType.GateB,
            ElevatorType.LczA,
            ElevatorType.LczB
        ];

        foreach (Lift lift in Lift.List)
        {
            if (lockEvTypes.Contains(lift.Type))
                lift.TryStart(0, false);
        }

        // エレベータードアロック
        foreach (Door door in Door.List)
        {
            if (door.Type is DoorType.ElevatorGateA or DoorType.ElevatorGateB or DoorType.ElevatorLczB or DoorType.ElevatorLczA)
            {
                door.IsOpen = false;
                door.Lock(DoorLockType.AdminCommand);
            }
        }

        // Cassieアナウンス
        Exiled.API.Features.Cassie.MessageTranslated(
            "All personnel . SCP 1 5 0 9 amnestic battle field simulation online .",
            "全職員に通達。SCP-1509 記憶処理戦闘シミュレーションを開始します。実践を想定して交戦してください。",
            true);
    }
}
