using System;
using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using MEC;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features; // SpecialEvent 基底
using UnityEngine;
using EventHandler = Slafight_Plugin_EXILED.MainHandlers.EventHandler;

namespace Slafight_Plugin_EXILED.SpecialEvents.Events;

public class DeltaWarheadEvent : SpecialEvent
{
    // ===== メタ情報 =====
#pragma warning disable CS0618
    public override SpecialEventType EventType => SpecialEventType.OldDeltaWarhead;
#pragma warning restore CS0618
    public override int MinPlayersRequired => 0;
    public override string LocalizedName => "DELTA WARHEAD";
    public override string TriggerRequirement => "無し";

    public override bool IsReadyToExecute()
    {
        return false;
    }

    // ===== ショートカット =====
    private EventHandler EventHandler => EventHandler.Instance;
    // ===== 実行本体 =====
    protected override void OnExecute(int eventPid)
    {
        if (CancelIfOutdated())
            return;

        // Warhead 関連フラグ
        Warhead.IsLocked = true;
        EventHandler.DeadmanDisable = true;
        // EventHandler.DeconCancellFlag = true; // 使うならここで

        if (CancelIfOutdated())
            return;

        // 8 秒後: 危険な SCiP 収容違反検知
        Timing.CallDelayed(8f, () =>
        {
            if (CancelIfOutdated()) return;

            Exiled.API.Features.Cassie.MessageTranslated(
                "Detected Danger SCPs Containment Breach in The Heavy Containment Zone. Thinking approaches...",
                "危険なSCiPの収容違反が中層で確認されました。対応策を考案します・・・",
                true);

            // 60 秒後: DELTA システム準備
            Timing.CallDelayed(60f, () =>
            {
                if (CancelIfOutdated()) return;

                Exiled.API.Features.Cassie.MessageTranslated(
                    "My Approaches confirmed by O5 Command, Setup Delta System...",
                    "対応策がO5評議会に承認されました。<color=green>DELTAシステム</color>を準備しています・・・",
                    true);

                // 180 秒後: DELTA WARHEAD シーケンス開始アナウンス
                Timing.CallDelayed(180f, () =>
                {
                    if (CancelIfOutdated()) return;

                    float boomTime = Plugin.Singleton.Config.DwBoomTime;

                    Exiled.API.Features.Cassie.MessageTranslated(
                        $"By Order of O5 Command . Delta Warhead Sequence Activated . Heavy Containment Zone Detonated in T MINUS {boomTime} Seconds.",
                        $"O5評議会の決定により、<color=green>DELTA WARHEAD</color>シーケンスが開始されました。重度収容区画を{boomTime}秒後に爆破します。",
                        true);

                    // HCZ を緑色に
                    foreach (Room room in Room.List)
                    {
                        if (room.Zone == ZoneType.HeavyContainment)
                            room.Color = Color.green;
                    }

                    // DELTA 用 BGM
                    SpeakerApi.Play("delta.ogg", "Exiled.API.Features.Cassie", Vector3.zero, true, null, false, 999999999f, 0f);

                    // DwBoomTime 後に実際の爆破処理
                    Timing.CallDelayed(boomTime, () =>
                    {
                        if (CancelIfOutdated()) return;

                        Log.Debug("Delta Passed EventPID Checker");

                        // LCZ エレベーター停止
                        List<ElevatorType> lockEvTypes =
                        [
                            ElevatorType.LczA,
                            ElevatorType.LczB
                        ];

                        foreach (Lift lift in Lift.List)
                        {
                            Log.Debug("sendforeach:" + lift.Type);
                            if (lockEvTypes.Contains(lift.Type))
                            {
                                Log.Debug("foreach catched: " + lift.Type);
                                lift.TryStart(0, true);
                            }
                        }

                        Log.Debug("Delta Passed TryStart Elevator Foreach.");

                        // チェックポイント / LCZ エレベーターをロック
                        List<DoorType> lockEvDoorTypes =
                        [
                            DoorType.CheckpointGateA,
                            DoorType.CheckpointGateB,
                            DoorType.ElevatorLczA,
                            DoorType.ElevatorLczB
                        ];

                        foreach (Door door in Door.List)
                        {
                            Log.Debug("lockforeach:" + door.Type);

                            if (door.Type is DoorType.CheckpointGateA or DoorType.CheckpointGateB)
                            {
                                door.IsOpen = false;
                            }

                            if (lockEvDoorTypes.Contains(door.Type))
                            {
                                Log.Debug("foreach catched: " + door.Type);
                                door.Lock(DoorLockType.AdminCommand);
                            }
                        }

                        Log.Debug("Delta Passed Lock Elevator Foreach.");

                        // HCZ / EZ にいるプレイヤーを爆破
                        foreach (Player player in Player.List)
                        {
                            Log.Debug("playerforeach:" + player.Zone);

                            if (player.Zone is ZoneType.Entrance or ZoneType.HeavyContainment)
                            {
                                player.ExplodeEffect(ProjectileType.FragGrenade);
                                player.Kill("DELTA WARHEADに爆破された");
                            }
                        }

                        Log.Debug("Delta Passed Kill Player Foreach");
                    });
                });
            });
        });
    }

    public override void RegisterEvents() { }
    public override void UnregisterEvents() { }
}
