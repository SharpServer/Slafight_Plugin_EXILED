using System;
using Exiled.API.Enums;
using Exiled.API.Features;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Extensions;

public static class RoomSpawner
{
    /// <summary>
    /// 指定 Room 内のローカル座標・角度にプレイヤーを配置します。
    /// </summary>
    /// <param name="player">配置するプレイヤー。</param>
    /// <param name="roomType">対象 RoomType。</param>
    /// <param name="localOffset">部屋ローカル座標オフセット。</param>
    /// <param name="localRotation">部屋ローカル回転 (EulerAngles)。</param>
    public static void SpawnInRoom(Player player, RoomType roomType, Vector3 localOffset, Vector3 localRotation = default)
    {
        Room targetRoom = Room.Get(roomType);
        if (targetRoom == null)
        {
            Log.Error($"Room {roomType} not found!");
            return;
        }

        // 部屋ワールド座標に変換
        Quaternion roomRot = targetRoom.Rotation;
        Vector3 worldPos = targetRoom.Position + roomRot * localOffset;
        
        // 回転: 部屋回転 * ローカル回転
        Quaternion spawnRot = roomRot * Quaternion.Euler(localRotation);

        player.Position = worldPos;
        player.Rotation = spawnRot;

        Log.Debug($"Spawned {player.Nickname} in {roomType} at local {localOffset}, rot {localRotation}");
    }

    /// <summary>
    /// 部屋中心 + Yオフセット配置 (簡易版)
    /// </summary>
    public static void SpawnRoomCenter(Player player, RoomType roomType, float yOffset = 1.5f)
    {
        SpawnInRoom(player, roomType, new Vector3(0, yOffset, 0));
    }
    
    /// <summary>
    /// ObjectSpawner.SpawnSchematic でシンプルスポーン (修正版)
    /// </summary>
    public static SchematicObject SpawnSchematicAtRoom(string schematicName, Room targetRoom, Vector3 localOffset, Vector3 localRotation = default)
    {
        try
        {
            Quaternion baseRot;
            Vector3 basePos;

            if (targetRoom != null)
            {
                baseRot = targetRoom.Rotation;
                basePos = targetRoom.Position;
                Log.Debug($"Spawned '{schematicName}' at {targetRoom.Type} local {localOffset}");
            }
            else
            {
                baseRot = Quaternion.identity;
                basePos = Vector3.zero;
                Log.Debug($"Spawned '{schematicName}' at World({localOffset})");
            }

            Vector3 spawnPos = basePos + baseRot * localOffset;
            Quaternion spawnRot = baseRot * Quaternion.Euler(localRotation);

            SchematicObject schem = ObjectSpawner.SpawnSchematic(schematicName, spawnPos);
            schem.Rotation = spawnRot;

            return schem;
        }
        catch (Exception ex)
        {
            Log.Error($"SpawnSchematic '{schematicName}' failed: {ex.Message}");
            return null;
        }
    }
}