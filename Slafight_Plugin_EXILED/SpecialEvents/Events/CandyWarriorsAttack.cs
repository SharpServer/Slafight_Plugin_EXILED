using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using Exiled.Events.EventArgs.Player;
using MEC;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.SpecialEvents.Events;

public class CandyWarriorsAttackEvent : SpecialEvent
{
    // ===== メタ情報 =====
    public override SpecialEventType EventType => SpecialEventType.CandyWarriorsAttack;
    public override int MinPlayersRequired => 5;
    public override string LocalizedName => "Candy Warriors Raid";
    public override string TriggerRequirement => "5人以上のプレイヤー";

    // ===== 内部状態 =====
    private bool _teslaDisabled = false;
    private string _warrierColor = "#ffffff";
    // ===== 実行エントリポイント =====
    public override bool IsReadyToExecute()
    {
        return MapFlags.GetSeason() is SeasonTypeId.April or SeasonTypeId.Halloween;
    }

    protected override void OnExecute(int eventPID)
    {
        _teslaDisabled = false;

        if (CancelIfOutdated()) return;

        Timing.RunCoroutine(RaidCoroutine());
    }

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Player.TriggeringTesla += DisableTesla;
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Player.TriggeringTesla -= DisableTesla;
    }

    // ===== メイン処理 =====
    private IEnumerator<float> RaidCoroutine()
    {
        RoundHazardController.SetAlphaWarheadDisarmLocked(true);
        RoundHazardController.SetDeadmanSwitchBlocked(true);
        RoundHazardController.DisableLightDecontamination();

        yield return Timing.WaitForSeconds(2f);
        if (CancelIfOutdated()) yield break;

        // カラー決定
        _warrierColor = MapFlags.GetSeason() is SeasonTypeId.April ? "#ff8cd9" : "#ff9633";

        // 役職変換
        foreach (var player in StaticUtils.SelectRandomPlayersByRatio(CTeam.SCPs, 1f / 3f))
        {
            player.SetRole(MapFlags.GetSeason() is SeasonTypeId.April
                ? CRoleTypeId.CandyWarriorApril
                : CRoleTypeId.CandyWarriorHalloween);
        }

        yield return Timing.WaitForSeconds(8f);
        if (CancelIfOutdated()) yield break;

        Exiled.API.Features.Cassie.MessageTranslated(
            "$pitch_1.02 Danger Detected Unknown Organism in Gate A . Please Check $pitch_.2 .g4 .g1 .g2",
            "警告、不明な生命体がGate Aで検出されました。確認を",
            true);

        yield return Timing.WaitForSeconds(12f);
        if (CancelIfOutdated()) yield break;

        Exiled.API.Features.Cassie.MessageTranslated(
            "$pitch_.8 Successfully terminated Foundations Cassie System and putted New Division Cassie System . Cassie is now under us",
            $"<color=#00b7eb>財団のCassieシステム</color>の<color=red>終了</color>に成功。新たな<color={_warrierColor}>お菓子の戦士たちのCassieシステム</color>の導入も成功。<split> Cassieは今や<b><color={_warrierColor}>お菓子の帝王</color></b>の手中にある。",
            false);

        yield return Timing.WaitForSeconds(45f);
        if (CancelIfOutdated()) yield break;

        Exiled.API.Features.Cassie.MessageTranslated(
            "$pitch_.8 First Order . Light up all facility . Accepted .",
            $"<b><color={_warrierColor}>お菓子の帝王</color></b>の最初の指令：全施設のライトアップ ...承認",
            false);

        Timing.RunCoroutine(LightUpCoroutine());

        yield return Timing.WaitForSeconds(8f);
        if (CancelIfOutdated()) yield break;

        Exiled.API.Features.Cassie.MessageTranslated(
            "$pitch_.8 Next Order . Turn off Tesla Gates . Accepted .",
            "次の指令：テスラゲートの無効化 ...承認",
            false);

        _teslaDisabled = true;

        yield return Timing.WaitForSeconds(8f);
        if (CancelIfOutdated()) yield break;

        Exiled.API.Features.Cassie.MessageTranslated(
            "$pitch_.8 All Division . Work Time .",
            "戦士達よ、働く時間だ。",
            false);

        yield return Timing.WaitForSeconds(1000f);
        if (CancelIfOutdated()) yield break;

        bool candyAlive = Player.List.Any(p =>
            p != null && p.GetCustomRole() is CRoleTypeId.CandyWarriorApril or CRoleTypeId.CandyWarriorHalloween);

        if (candyAlive)
            Timing.RunCoroutine(CandySuccessCoroutine());
        else
            HandleCandyWarriorFailure();
    }

    // ===== 成功時 =====
    private IEnumerator<float> CandySuccessCoroutine()
    {
        if (CancelIfOutdated()) yield break;

        Exiled.API.Features.Cassie.MessageTranslated(
            "$pitch_.8 All Division Agents Tasks completed . Last Order . . $pitch_.75 Destroy the Facility . $pitch_.4 .g1 $pitch_.26 .g5 .g6 .g4 $pitch_2 .g1 $pitch_.75 Good by all anomalys and foundation personnels .",
            $"全戦士達の任務完了を確認。最後の指令を下す：<b><color={_warrierColor}>施設を爆破させよ</color></b>",
            true);

        yield return Timing.WaitForSeconds(15f);
        if (CancelIfOutdated()) yield break;

        Exiled.API.Features.Cassie.MessageTranslated(
            "$pitch_.2 .g4 .g4 $pitch_1 $pitch_.75 BY ORDER OF DIVISION COMMAND . THE DEAD MANS SEQUENCE AND ATTACK PROTOCOL ACTIVATED . DETONATION IN TMINUS 145 SECONDS . PLEASE B .g4 O .g6 .g3 .g4",
            $"BY ORDER OF <color={_warrierColor}><b>DIVISION COMMAND</b></color>. THE DEAD MANS SEQUENCE AND PINK CANDY ATTACK PROTOCOL ACTIVATED. DETONATION IN T-145 SECONDS. <color=red><b>PLEASE BOOM</b></color>",
            true);

        yield return Timing.WaitForSeconds(10f);
        if (CancelIfOutdated()) yield break;

        SpeakerApi.Play("cir.ogg", "Cassie", Vector3.zero, true, null, false, 999999999f, 0f);

        SchematicObject schematicObject;
        try
        {
            schematicObject = ObjectSpawner.SpawnSchematic("Candy_Nuke", Vector3.zero);
        }
        catch (Exception)
        {
            yield break;
        }

        yield return Timing.WaitForSeconds(0.5f);

        if (schematicObject == null) yield break;

        schematicObject.Position = new Vector3(-90f, 500f, -45f);
        schematicObject.Rotation = Quaternion.Euler(new Vector3(0, 0, 55));
        Timing.RunCoroutine(NukeDownCoroutine(schematicObject));

        ColorUtility.TryParseHtmlString("#ff4fad", out var roomColor);
        foreach (var room in Room.List)
        {
            room.AreLightsOff = false;
            room.Color = roomColor;
        }

        foreach (var door in Door.List)
        {
            if (door.Type is DoorType.ElevatorGateA or DoorType.ElevatorGateB
                          or DoorType.ElevatorLczA  or DoorType.ElevatorLczB
                          or DoorType.ElevatorNuke  or DoorType.ElevatorScp049
                          or DoorType.ElevatorServerRoom)
                continue;

            door.IsOpen = true;
            door.Lock(DoorLockType.Warhead);
        }

        yield return Timing.WaitForSeconds(145f);
        if (CancelIfOutdated()) yield break;

        foreach (var player in Player.List)
        {
            if (player == null || !player.IsAlive) continue;

            player.ExplodeEffect(ProjectileType.FragGrenade);
            player.Kill(player.Zone == ZoneType.Surface
                ? "MEGABALL ATTACKに爆破された"
                : "ALPHA WARHEADに爆破された");
        }
    }

    // ===== 失敗時 =====
    private void HandleCandyWarriorFailure()
    {
        if (CancelIfOutdated()) return;

        Exiled.API.Features.Cassie.MessageTranslated(
            "$pitch_.2 .g3 $pitch_.7 .g2 $pitch_.4 .g4 .g5 .g5 $pitch_1 .g1 .g2 .g3 Attention . All personnel . the Foundation Forces Successfully Terminated All Forces . All System now backed to the Foundation . All Division Command Orders Now Terminated . Please back to normal Containment Breach Security Mode",
            "全職員に報告します。財団の部隊は全お菓子の戦士達勢力の排除に成功しました。全てのDIVISION COMMANDの指令は正常に終了。全職員は収容違反の対応モデルに復帰してください。",
            true);
    }

    // ===== Tesla 無効化 =====
    private void DisableTesla(TriggeringTeslaEventArgs ev)
    {
        if (CancelIfOutdated()) return;
        ev.DisableTesla = _teslaDisabled;
    }

    // ===== コルーチン =====
    private IEnumerator<float> NukeDownCoroutine(SchematicObject schem)
    {
        if (schem == null || schem.transform == null)
        {
            Log.Warn("[Candy Raid] NukeDown aborted: schem or transform is null at start.");
            yield break;
        }

        float elapsedTime = 0f;
        const float totalDuration = 150f;
        float z = schem.transform.position.z;
        Vector3 startPos = new Vector3(-90f, 500f, z);
        Vector3 endPos   = new Vector3( 70f, 300f, z);

        while (elapsedTime < totalDuration)
        {
            if (CancelIfOutdated() || Round.IsLobby || Round.IsEnded)
            {
                Log.Info("[Candy Raid] NukeDown stopped: event outdated or round ended.");
                yield break;
            }

            if (schem == null || schem.transform == null)
            {
                Log.Warn("[Candy Raid] NukeDown stopped: schem destroyed.");
                yield break;
            }

            elapsedTime += Time.deltaTime;
            schem.transform.position = Vector3.Lerp(startPos, endPos, elapsedTime / totalDuration);

            yield return 0f;
        }

        if (schem != null && schem.transform != null)
            schem.transform.position = endPos;
    }

    private IEnumerator<float> LightUpCoroutine()
    {
        ColorUtility.TryParseHtmlString(_warrierColor, out var color);

        for (;;)
        {
            if (CancelIfOutdated()) yield break;

            foreach (var room in Room.List)
            {
                room.AreLightsOff = false;
                room.Color = color;
            }

            yield return Timing.WaitForSeconds(30f);
        }
    }
}
