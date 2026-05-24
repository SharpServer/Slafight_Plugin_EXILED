using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using MEC;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.Hints;
using Slafight_Plugin_EXILED.MainHandlers;
using UnityEngine;
using EventHandler = Slafight_Plugin_EXILED.MainHandlers.EventHandler;

namespace Slafight_Plugin_EXILED.SpecialEvents.Events;

public class FacilityTermination : SpecialEvent
{
    public override SpecialEventType EventType => SpecialEventType.FacilityTermination;
    public override int MinPlayersRequired => 8;
    public override string LocalizedName => "FACILITY TERMINATION";
    public override string TriggerRequirement => "無し";

    private CoroutineHandle _mainCoroutine;

    private static EventHandler EventHandler => EventHandler.Instance;
    private Action<string, string, Vector3, bool, Transform, bool, float, float> CreateAndPlayAudio =>
        EventHandler.CreateAndPlayAudio;

    // ===== エントリーポイント =====

    protected override void OnExecute(int eventPid)
    {
        Warhead.IsLocked = true;
        EventHandler.DeadmanDisable = true;
        EventHandler.DeconCancellFlag = true;

        SpawnContextRegistry.SetActive("FacilityTerminationCustom");

        Timing.KillCoroutines(_mainCoroutine);
        _mainCoroutine = Timing.RunCoroutine(DecontaminationCoroutine());
    }

    // ===== キャンセル判定 =====

    /// <summary>
    /// イベントがキャンセル済みならクリーンアップして true を返す。
    /// コルーチン内で if (IsEventCanceled()) yield break; のように使う。
    /// </summary>
    private bool IsEventCanceled()
    {
        if (!CancelIfOutdated()) return false;

        Exiled.API.Features.Cassie.Clear();
        Timing.KillCoroutines(_mainCoroutine);
        SpawnContextRegistry.SetActive("Default");
        return true;
    }

    // ===== エレベーター＆チェックポイント制御 =====

    private static void SetElevatorLockByZone(ZoneType zone, bool locked)
    {
        foreach (var door in Door.List)
        {
            if (!door.IsElevator) continue;

            bool isTarget = zone switch
            {
                ZoneType.Surface        => door.Type is DoorType.ElevatorGateA or DoorType.ElevatorGateB or DoorType.ElevatorNuke,
                ZoneType.Entrance       => door.Type is DoorType.ElevatorGateA or DoorType.ElevatorGateB or DoorType.ElevatorScp049,
                ZoneType.HeavyContainment => door.Type is DoorType.ElevatorScp049,
                ZoneType.LightContainment => door.Type is DoorType.ElevatorLczA or DoorType.ElevatorLczB,
                _                       => false
            };

            if (!isTarget) continue;

            ApplyDoorLock(door, locked);
        }
    }

    private static void SetCheckpointLock(bool locked)
    {
        foreach (var door in Door.List)
        {
            if (door.Type is DoorType.CheckpointLczA or DoorType.CheckpointLczB)
                ApplyDoorLock(door, locked);
        }
    }

    private static void ApplyDoorLock(Door door, bool locked)
    {
        if (locked)
        {
            door.IsOpen = false;
            door.Lock(DoorLockType.AdminCommand);
        }
        else
        {
            door.Unlock();
            door.IsOpen = true;
        }
    }

    // ===== 全ゾーン開放 =====

    private static void OpenAllZones()
    {
        foreach (ZoneType zone in new[] { ZoneType.Surface, ZoneType.Entrance, ZoneType.HeavyContainment, ZoneType.LightContainment })
            SetElevatorLockByZone(zone, false);

        SetCheckpointLock(false);

        foreach (var room in Room.List)
        {
            room.Color = Color.green;
            room.UnlockAll();
            foreach (var door in room.Doors)
            {
                door.Unlock();
                door.IsOpen = true;
            }
        }
    }

    // ===== 除染本体 =====

    private IEnumerator<float> DecontaminationCoroutine()
    {
        if (IsEventCanceled()) yield break;

        // SCP全173化
        int scpCount = 0;
        foreach (var player in Player.List)
        {
            if (player?.GetTeam() == CTeam.SCPs)
            {
                player.SetRole(CRoleTypeId.Sculpture);
                scpCount++;
            }
        }
        Log.Debug($"[FacilityTermination] Converted {scpCount} SCPs to Sculpture");

        yield return Timing.WaitForSeconds(2f);
        if (IsEventCanceled()) yield break;

        // ブロードキャスト
        PlayerHUD.Instance.ForceUpdateAll();
        foreach (var player in Player.List)
        {
            if (player == null) continue;
            player.Broadcast(8,
                player.IsHumanitist()
                    ? "<size=28>あなたは<color=blue>人類陣営</color>です。\n警備員と彫刻以外が一応仲間です。狂気を生き延びて奴らを終了してください！</size>"
                    : "<size=28>あなたは<color=red>正常性陣営</color>です。\n警備員と彫刻が仲間です。他の奴らを全員終了してください！</size>");
        }

        Exiled.API.Features.Cassie.MessageTranslated(
            "Attention, All personnel. Were recieved message from O5 Command. Please Red this and Terminate Human it Your Self.",
            "全職員に通達。O5評議会からメッセージを受信した為、お知らせします。これをよく読み、自身の人間性を<color=green>破壊</color>してください。<split>以下はO5評議会の総意によって作成されたメッセージです。<split>現時点で私たちの存在を知らない方々へ: 私たちはSCP財団という組織を代表しています。私たちのかつての使命は、異常な事物、実体、その他様々な現象の収容と研究を中心に展開されていました。この使命は過去100年以上にわたって私たちの組織の焦点でした。<split>やむを得ない事情により、この方針は変更されました。私たちの新たな使命は人類の根絶です。<split>今後の意思疎通は行われません。",
            true);

        yield return Timing.WaitForSeconds(25f);
        if (IsEventCanceled()) yield break;

        CassieHelper.AnnounceLastOperationArrival();
        SpawnSystem.ReplaceNextSpawn(SpawnTypeId.GoiGoCNormal);

        // ===== 22分 待機（イベントのメイン猶予） =====
        yield return Timing.WaitForSeconds(1320f);
        if (IsEventCanceled()) yield break;

        // 避難フェーズ開始アナウンス
        Exiled.API.Features.Cassie.MessageTranslated(
            "Attention, All personnel. Were decided Decontamination of the Facility. Please Evacuate to the Surface for Delta Protocol.",
            "全職員に通達。施設全体の<color=yellow>終了</color>が決定された為、これより軽度収容区画～エントランス区画の<color=red>ロックダウン</color>及び<color=green>除染プロセス</color>を開始します。全職員は地上に避難し、<color=green><b>DELTAプロトコル</b></color>を待機してください。");

        yield return Timing.WaitForSeconds(12.5f);
        if (IsEventCanceled()) yield break;

        // 全ゾーン開放
        OpenAllZones();
        CreateAndPlayAudio("newdelta.ogg", "DeltaWarhead", Vector3.zero, true, null, false, 999999999f, 0f);

        // ===== LCZ 除染フェーズ =====
        yield return Timing.WaitForSeconds(10f);
        if (IsEventCanceled()) yield break;

        Exiled.API.Features.Cassie.MessageTranslated(
            "Light Containment Zone Lockdown and Decontamination in T minus 80 Seconds.",
            "軽度収容区画のロックダウン及び除染まで残り: 80秒", false, false);

        yield return Timing.WaitForSeconds(75f);
        if (IsEventCanceled()) yield break;

        LockdownAndDecon(ZoneType.LightContainment);
        SetElevatorLockByZone(ZoneType.LightContainment, true);

        Exiled.API.Features.Cassie.MessageTranslated(
            "Light Containment Zone is now Lockdowned and Started Decontamination Process.",
            "軽度収容区画がロックダウンされ、除染が開始されました。", false, false);

        // ===== HCZ 除染フェーズ =====
        if (IsEventCanceled()) yield break;

        Exiled.API.Features.Cassie.MessageTranslated(
            "Heavy Containment Zone Lockdown and Decontamination in T minus 40 Seconds.",
            "重度収容区画のロックダウン及び除染まで残り: 40秒", false, false);

        yield return Timing.WaitForSeconds(35f);
        if (IsEventCanceled()) yield break;

        LockdownAndDecon(ZoneType.HeavyContainment);
        SetElevatorLockByZone(ZoneType.HeavyContainment, true);

        Exiled.API.Features.Cassie.MessageTranslated(
            "Heavy Containment Zone is now Lockdowned and Started Decontamination Process.",
            "重度収容区画がロックダウンされ、除染が開始されました。", false, false);

        // ===== Entrance 除染フェーズ =====
        if (IsEventCanceled()) yield break;

        Exiled.API.Features.Cassie.MessageTranslated(
            "Entrance Zone Lockdown and Decontamination in T minus 20 Seconds.",
            "エントランス区画のロックダウン及び除染まで残り: 20秒", false, false);

        yield return Timing.WaitForSeconds(15f);
        if (IsEventCanceled()) yield break;

        LockdownAndDecon(ZoneType.Entrance);
        SetElevatorLockByZone(ZoneType.Entrance, true);

        Exiled.API.Features.Cassie.MessageTranslated(
            "Entrance Zone is now Lockdowned and Started Decontamination Process.",
            "エントランス区画がロックダウンされ、除染が開始されました。", false, false);

        // ===== DELTA PROTOCOL =====
        if (IsEventCanceled()) yield break;

        Exiled.API.Features.Cassie.MessageTranslated(
            "Attention, All personnel. Delta Protocol is started in Surface and Detonate in T minus 100 seconds. Please Effect by Delta Protocol Warhead. See you human.",
            "全職員に通達。<color=green><b>DELTAプロトコル</b></color>が地上にて開始されました。<split>100秒後に爆破される、<b><color=green>DELTA PROTOCOL</color> <color=red>\"WARHEAD\"</color></b>の影響を受け、人間性を<color=yellow>終了</color>してください。");

        SetElevatorLockByZone(ZoneType.Surface, true);
        SetCheckpointLock(true);

        yield return Timing.WaitForSeconds(130f);
        if (IsEventCanceled()) yield break;

        // 爆発終了処理
        AlphaWarheadController.Singleton.RpcShake(false);
        CTeam.FoundationForces.EndRound();
        foreach (var player in Player.List.Where(p => p.IsAlive).ToList())
        {
            player.ExplodeEffect(ProjectileType.FragGrenade);
            player.Kill("DELTA WARHEADに爆破された");
        }
    }

    // ===== ゾーンロックダウン＋除染付与 =====

    private static void LockdownAndDecon(ZoneType zone)
    {
        foreach (var room in Room.List.Where(r => r.Zone == zone))
            room.LockDown(-1, DoorLockType.DecontLockdown);

        foreach (var player in Player.List.Where(p => p.Zone == zone && p.IsAlive))
            player.EnableEffect(EffectType.Decontaminating);
    }

    // FacilityTermination の勝利判定は RoundVictoryDefinitions 側で全体判定する。
}
