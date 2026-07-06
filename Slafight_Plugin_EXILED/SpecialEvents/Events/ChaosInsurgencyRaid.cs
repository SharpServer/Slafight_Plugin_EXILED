using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp079;
using MEC;
using PlayerRoles;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Features.RoundVictory.Core;
using Slafight_Plugin_EXILED.Changes;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.CustomMaps.Features;
using Slafight_Plugin_EXILED.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Slafight_Plugin_EXILED.SpecialEvents.Events;

public class ChaosInsurgencyRaidEvent : SpecialEvent
{
    // ===== メタ情報 =====
    public override SpecialEventType EventType => SpecialEventType.NuclearAttack;
    public override int MinPlayersRequired => 5;
    public override string LocalizedName => "Chaos Insurgency Raid";
    public override string TriggerRequirement => "5人以上のプレイヤー";

    // ===== 内部状態 =====
    private bool _teslaDisabled = false;
    // ===== 実行エントリポイント =====
    public override bool IsReadyToExecute()
    {
        return MapFlags.GetSeason() == SeasonTypeId.None;
    }

    protected override void OnExecute(int eventPID)
    {
        _teslaDisabled = false;

        if (CancelIfOutdated()) return;

        Timing.RunCoroutine(RaidCoroutine());
    }

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Scp079.Recontaining += OnRecontained;
        Exiled.Events.Handlers.Player.TriggeringTesla += DisableTesla;
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Scp079.Recontaining -= OnRecontained;
        Exiled.Events.Handlers.Player.TriggeringTesla -= DisableTesla;
    }

    // ===== メイン処理 =====
    private IEnumerator<float> RaidCoroutine()
    {
        // Warhead ロックなど
        RoundHazardController.SetAlphaWarheadDisarmLocked(true);
        RoundHazardController.SetDeadmanSwitchBlocked(true);
        
        yield return Timing.WaitForSeconds(2f);
        if (CancelIfOutdated()) yield break;

        Respawn.SummonChaosInsurgencyVan();
        Respawn.SummonNtfChopper();
        // カオスに変える対象を抽選
        foreach (var player in StaticUtils.SelectRandomPlayersByRatio(CTeam.SCPs, 1f / 3f, true))
            player.SetRole(CRoleTypeId.ChaosCommando);

        yield return Timing.WaitForSeconds(8f);
        if (CancelIfOutdated()) yield break;

        Exiled.API.Features.Cassie.MessageTranslated(
            "$pitch_1.02 Danger Detected Unknown Forces in Gate A . Please Check $pitch_.2 .g4 .g1 .g2",
            "警告、不明な部隊がGate Aで検出されました。確認を",
            true);

        yield return Timing.WaitForSeconds(12f);
        if (CancelIfOutdated()) yield break;

        Exiled.API.Features.Cassie.MessageTranslated(
            "$pitch_.8 Successfully terminated Foundations Cassie System and putted New Insurgencys Cassie System . Cassie is now under delta command",
            "<color=#00b7eb>財団のCassieシステム</color>の<color=red>終了</color>に成功。新たな<color=#228b22>インサージェンシーのCassieシステム</color>の導入も成功。<split> Cassieは今や<b><color=#228b22>DELTA COMMAND</color></b>の手中にある。",
            false);

        yield return Timing.WaitForSeconds(45f);
        if (CancelIfOutdated()) yield break;

        Exiled.API.Features.Cassie.MessageTranslated(
            "$pitch_.8 First Order of Delta Command . Turn off all facilitys . Accepted .",
            "<b><color=#228b22>DELTA COMMAND</color></b>の最初の指令：全施設の消灯 ...承認",
            false);

        foreach (var room in Room.List)
            room.Color = new Color(55 / 255f, 55 / 255f, 55 / 255f);
        
        CTeam.SCPs.SetNightVisionTargetStateForTeam(true);


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
            "$pitch_.8 All Insurgency Agents . Work Time .",
            "インサージェンシーのエージェント達よ、働く時間だ。",
            false);

        yield return Timing.WaitForSeconds(1000f);
        if (CancelIfOutdated()) yield break;

        bool ciAlive = Player.List.Any(p => p != null && p.GetTeam() == CTeam.ChaosInsurgency);

        if (ciAlive)
            Timing.RunCoroutine(CiSuccessCoroutine());
        else
            HandleCiFailure();
    }

    // ===== 成功時（施設破壊 / 攻撃プロトコル） =====
    private IEnumerator<float> CiSuccessCoroutine()
    {
        if (CancelIfOutdated()) yield break;

        Exiled.API.Features.Cassie.MessageTranslated(
            "$pitch_.8 All Insurgency Agents Tasks completed . Last Order . . $pitch_.75 Destroy the Facility . $pitch_.4 .g1 $pitch_.26 .g5 .g6 .g4 $pitch_2 .g1 $pitch_.75 Good by all anomalys and foundation personnels .",
            "全インサージェンシーエージェントの任務完了を確認。最後の指令を下す：<b><color=red>施設を破壊せよ</color></b>",
            true);

        yield return Timing.WaitForSeconds(15f);
        if (CancelIfOutdated()) yield break;

        Exiled.API.Features.Cassie.MessageTranslated(
            "$pitch_.2 .g4 .g4 $pitch_1 $pitch_.75 BY ORDER OF DELTA COMMAND . THE DEAD MANS SEQUENCE AND SURFACE ATTACK PROTOCOL ACTIVATED . DETONATION IN TMINUS 145 SECONDS . ",
            "BY ORDER OF <color=#228b22><b>DELTA COMMAND</b></color>. THE DEAD MANS SEQUENCE AND SURFACE ATTACK PROTOCOL ACTIVATED. DETONATION IN T-145 SECONDS. ",
            true);

        yield return Timing.WaitForSeconds(10f);
        if (CancelIfOutdated()) yield break;

        SpeakerApi.Play("cir.ogg", "Cassie", Vector3.zero, true, null, false, 999999999f, 0f);

        SchematicObject schematicObject;
        try
        {
            schematicObject = ObjectSpawner.SpawnSchematic("Nuke", Vector3.zero);
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

        foreach (var room in Room.List)
        {
            room.AreLightsOff = false;
            room.Color = new Color32(255, 0, 0, 255);
        }
        
        CTeam.SCPs.SetNightVisionTargetStateForTeam(false);

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

        EvacuationRoundEndState.Begin();
        EscapeHandler.AddEscapeOverride(p => new EscapeHandler.EscapeTargetRole { Vanilla = RoleTypeId.Spectator });
        Exiled.API.Features.Cassie.MessageTranslated(
            "This is O5 Message from the Site 1, For All personnel, Please escape from the facility .",
            "[Site-01, O5からの通信]全職員へ通達：救助部隊を派遣しました。直ちに<color=green>脱出口</color>から<color=yellow>脱出</color>してください。");

        yield return Timing.WaitForSeconds(140f);
        if (CancelIfOutdated()) yield break;

        WarheadDoorLockdown.LockAllDoorsClosed();

        yield return Timing.WaitForSeconds(5f);
        if (CancelIfOutdated()) yield break;

        foreach (var player in Player.List)
        {
            if (player == null || !player.IsAlive) continue;

            player.ExplodeEffect(ProjectileType.FragGrenade);
            player.Kill(player.Zone == ZoneType.Surface
                ? "SURFACE ATTACK PROTOCOL に爆破された"
                : "ALPHA WARHEADに爆破された");
        }
    }

    // ===== 失敗時（財団勝利） =====
    private void HandleCiFailure()
    {
        if (CancelIfOutdated()) return;
        _teslaDisabled = false;
        Exiled.API.Features.Cassie.MessageTranslated(
            "$pitch_.2 .g3 $pitch_.7 .g2 $pitch_.4 .g4 .g5 .g5 $pitch_1 .g1 .g2 .g3 Attention . All personnel . the Foundation Forces Successfully Terminated All Chaos Insurgency Forces . All System now backed to the Foundation . All Delta Command Orders Now Terminated . Please back to normal Containment Breach Security Mode",
            "全職員に報告します。財団の部隊は全カオス・インサージェンシー勢力の排除に成功しました。全てのDELTA COMMANDの指令は正常に終了。全職員は収容違反の対応モデルに復帰してください。",
            true);
    }

    private void OnRecontained(RecontainingEventArgs ev)
    {
        if (CancelIfOutdated() || !ev.IsAllowed) return;
        _teslaDisabled = false;
        FacilityLightHandler.TurnToNormal();
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
            Log.Warn("[CI Raid] NukeDown aborted: schem or transform is null at start.");
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
                Log.Info("[CI Raid] NukeDown stopped: event outdated or round ended.");
                yield break;
            }

            if (schem == null || schem.transform == null)
            {
                Log.Warn("[CI Raid] NukeDown stopped: schem destroyed.");
                yield break;
            }

            elapsedTime += Time.deltaTime;
            schem.transform.position = Vector3.Lerp(startPos, endPos, elapsedTime / totalDuration);

            yield return 0f;
        }

        if (schem != null && schem.transform != null)
            schem.transform.position = endPos;
    }
}
