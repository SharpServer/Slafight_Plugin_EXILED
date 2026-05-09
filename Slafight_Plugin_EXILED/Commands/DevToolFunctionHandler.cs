using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using MEC;
using PlayerRoles;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.SpecialEvents;
using UnityEngine;
using EventHandler = Slafight_Plugin_EXILED.MainHandlers.EventHandler;
using OmegaWarhead = Slafight_Plugin_EXILED.CustomMaps.Features.OmegaWarhead;
using Npc = Exiled.API.Features.Npc;

namespace Slafight_Plugin_EXILED.Commands;

public class DevToolFunctionHandler
{
    private readonly Action<string, string, Vector3, bool, Transform, bool, float, float> CreateAndPlayAudio =
        EventHandler.CreateAndPlayAudio;

    public void PlaySurfaceAttack()
    {
        Exiled.API.Features.Cassie.MessageTranslated(
            "$pitch_.8 All Insurgency Agents Tasks completed . Last Order . . $pitch_.75 Destroy the Facility . $pitch_.4 .g1 $pitch_.26 .g5 .g6 .g4 $pitch_2 .g1 $pitch_.75 Good by all anomalys and foundation personnels .",
            "全インサージェンシーエージェントの任務完了を確認。最後の指令を下す：<b><color=red>施設を破壊せよ</color></b>");

        Timing.CallDelayed(15f, () =>
        {
            Exiled.API.Features.Cassie.MessageTranslated(
                "$pitch_.2 .g4 .g4 $pitch_1 $pitch_.75 BY ORDER OF DELTA COMMAND . THE DEAD MANS SEQUENCE AND SURFACE ATTACK PROTOCOL ACTIVATED . DETONATION IN TMINUS 145 SECONDS . PLEASE D .g4 IE .g6 .g3 .g4",
                "BY ORDER OF <color=#228b22><b>DELTA COMMAND</b></color>. THE DEAD MANS SEQUENCE AND SURFACE ATTACK PROTOCOL ACTIVATED. DETONATION IN T-145 SECONDS. <color=red><b>PLEASE DIE</b></color>",
                false,
                false);

            Timing.CallDelayed(10f, () =>
            {
                CreateAndPlayAudio("cir.ogg", "Exiled.API.Features.Cassie",
                    Vector3.zero, true, null, false, 999999999, 0);

                SchematicObject schematicObject;
                try
                {
                    schematicObject = ObjectSpawner.SpawnSchematic("Nuke", Vector3.zero);
                }
                catch (Exception)
                {
                    schematicObject = null;
                    return;
                }

                Timing.CallDelayed(0.5f, () =>
                {
                    schematicObject.Position = new Vector3(-90f, 500f, -45f);
                    schematicObject.Rotation = Quaternion.Euler(new Vector3(0, 0, 55));
                    Timing.RunCoroutine(NukeDownCoroutine(schematicObject));
                });

                foreach (Room room in Room.List)
                {
                    room.AreLightsOff = false;
                    room.Color = new Color32(255, 0, 0, 255);
                }

                foreach (Door door in Door.List)
                {
                    if (door.Type != DoorType.ElevatorGateA &&
                        door.Type != DoorType.ElevatorGateB &&
                        door.Type != DoorType.ElevatorLczA &&
                        door.Type != DoorType.ElevatorLczB &&
                        door.Type != DoorType.ElevatorNuke &&
                        door.Type != DoorType.ElevatorScp049 &&
                        door.Type != DoorType.ElevatorServerRoom)
                    {
                        door.IsOpen = true;
                        door.Lock(DoorLockType.Warhead);
                    }
                }

                Timing.CallDelayed(145f, () =>
                {
                    foreach (Player player in Player.List)
                    {
                        if (player == null) continue;

                        player.ExplodeEffect(ProjectileType.FragGrenade);

                        if (player.Zone == ZoneType.Surface)
                            player.Kill("SURFACE ATTACKに爆破された");
                        else
                            player.Kill("ALPHA WARHEADに爆破された");
                    }
                });
            });
        });
    }

    private IEnumerator<float> NukeDownCoroutine(SchematicObject schem)
    {
        // 開始時点での安全確認
        if (schem == null || schem.transform == null)
        {
            Log.Warn("[DevTool] NukeDown aborted: schem or transform is null at start.");
            yield break;
        }

        float elapsedTime = 0f;
        const float totalDuration = 150f;
        float z = schem.transform.position.z;
        Vector3 startPos = new Vector3(-90f, 500f, z);
        Vector3 endPos   = new Vector3( 70f, 300f, z);

        while (elapsedTime < totalDuration)
        {
            // ラウンド終了・再起動中などなら即終了
            if (Round.IsLobby || Round.IsEnded)
            {
                Log.Info("[DevTool] NukeDown stopped: round lobby/ended.");
                yield break;
            }

            // Schematic が途中で消えた場合も即終了
            if (schem == null || schem.transform == null)
            {
                Log.Warn("[DevTool] NukeDown stopped: schem destroyed.");
                yield break;
            }

            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / totalDuration;
            schem.transform.position = Vector3.Lerp(startPos, endPos, progress);

            yield return 0f;
        }

        if (schem != null && schem.transform != null)
            schem.transform.position = endPos;
    }

    public void PlayOmegaWarhead()
    {
        OmegaWarhead.StartProtocol(SpecialEventsHandler.Instance.EventPID);
    }

    public void DebugRoundStart(Player startedPlayer)
    {
        SpecialEventsHandler.Instance.SetQueueEvent(SpecialEventType.None);
        Npc roundLock = Npc.Spawn("RoundLocker", RoleTypeId.ClassD);
        Npc roundLock2 = Npc.Spawn("RoundLocker", RoleTypeId.FacilityGuard);
        Round.Start();

        Timing.CallDelayed(2f, () =>
        {
            // デバッグプレイヤー設定
            startedPlayer.SetRole(RoleTypeId.Tutorial);
            startedPlayer.UniqueRole = "Debug";
            startedPlayer.AddItem(ItemType.Coin);
            startedPlayer.TryAddCustomItem(1000);
            startedPlayer.IsGodModeEnabled = true;
            startedPlayer.Position = new Vector3(39.48081f, 314.1123f, -32.159f);
            startedPlayer.ShowHint(
                "デバッグラウンドを開始しました！\n" +
                "あなたはデバッグツールが使用可能です！\n" +
                "コイン - 現在の座標・部屋を取得できます\n" +
                "ドア操作時 - ドアの名前等が分かります\n" +
                "Dummy Road Spawner - 大量のダミーDクラスを出せます",
                10f);
                
            // まず D クラス NPC としてスポーン
            var rtdNpcs   = SpawnAllRoleTypeIdNpcs(startedPlayer);
            var croleNpcs = SpawnAllCRoleNpcs(startedPlayer);

            // ロール確定
            Timing.CallDelayed(1f, () =>
            {
                ApplyAllRoleTypeIdRoles(rtdNpcs);
                ApplyAllCRoleRoles(croleNpcs);
            });

            // 位置整列
            Timing.CallDelayed(3f, () =>
            {
                var allNpcs = new List<Npc>();

                foreach (var (npc, _) in rtdNpcs)
                    if (npc != null && npc.IsConnected)
                        allNpcs.Add(npc);

                foreach (var (npc, _) in croleNpcs)
                    if (npc != null && npc.IsConnected)
                        allNpcs.Add(npc);

                allNpcs.ForEach(npc => npc.IsGodModeEnabled = true);
                ArrangeNpcsRing(allNpcs, startedPlayer.Position, 3.0f);
                roundLock.Destroy();
                roundLock2.Destroy();
            });
        });
    }

    // RoleTypeId NPC

    private List<(Npc npc, RoleTypeId roleId)> SpawnAllRoleTypeIdNpcs(Player center)
    {
        var list = new List<(Npc, RoleTypeId)>();

        foreach (RoleTypeId roleId in Enum.GetValues(typeof(RoleTypeId)))
        {
            if (roleId is RoleTypeId.None or RoleTypeId.Destroyed)
                continue;

            var npc = Npc.Spawn($"RTD: {roleId}", RoleTypeId.ClassD);
            if (npc is null)
                continue;

            list.Add((npc, roleId));
        }

        return list;
    }

    private void ApplyAllRoleTypeIdRoles(IEnumerable<(Npc npc, RoleTypeId roleId)> npcs)
    {
        foreach (var (npc, roleId) in npcs)
        {
            if (npc == null || !npc.IsConnected) continue;
            npc.SetRole(roleId);
        }
    }

    // CRole NPC

    private List<(Npc npc, CRoleTypeId cId)> SpawnAllCRoleNpcs(Player center)
    {
        var list = new List<(Npc, CRoleTypeId)>();

        foreach (CRoleTypeId cId in Enum.GetValues(typeof(CRoleTypeId)))
        {
            // ★ Destroyed 的な「スポーン対象外」をここでスキップする
            if (cId == CRoleTypeId.None) // 例: 並べたくないロール
                continue;

            var npc = Npc.Spawn($"CRole: {cId}", RoleTypeId.ClassD);
            if (npc is null)
                continue;

            list.Add((npc, cId));
        }

        return list;
    }

    private void ApplyAllCRoleRoles(IEnumerable<(Npc npc, CRoleTypeId cId)> npcs)
    {
        foreach (var (npc, cId) in npcs)
        {
            if (npc == null || !npc.IsConnected) continue;

            try
            {
                npc.SetRole(cId, RoleSpawnFlags.All);
            }
            catch (Exception ex)
            {
                Log.Error($"ApplyAllCRoleRoles: SetRole({cId}) failed: {ex}");
            }
        }
    }

    // 並べ替え

    private void ArrangeNpcsRing(IEnumerable<Npc> npcs, Vector3 center, float radius)
    {
        var arr = npcs.Where(n => n != null && n.IsConnected).ToArray();
        int count = arr.Length;
        if (count == 0)
            return;

        for (int i = 0; i < count; i++)
        {
            var npc = arr[i];
            float angle = (Mathf.PI * 2f / count) * i;
            var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            npc.Position = center + offset;
        }
    }
}