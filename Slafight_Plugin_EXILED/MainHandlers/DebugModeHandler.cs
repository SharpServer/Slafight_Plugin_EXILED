using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.MainHandlers;

public static class DebugModeHandler
{
    // =====================
    //  デバッグモード管理
    // =====================

    private static readonly HashSet<Player> DebugModePlayers = [];

    /// <summary>指定プレイヤーのデバッグモードが ON かどうか。</summary>
    public static bool IsDebugMode(Player player)
    {
        return player.CheckPermission(PlayerPermissions.Noclip) && DebugModePlayers.Contains(player);
    }

    // =====================
    //  デバッグ状態ストア
    // =====================

    /// <summary>最後に触ったドアのスナップショット。</summary>
    public readonly record struct DoorInfo(
        string  DoorType,
        string  DoorName,
        string  RoomType,
        Vector3 LocalPos,
        Vector3 LocalEuler,
        Vector3 RoomEuler
    );

    private static readonly Dictionary<int, DoorInfo> _lastDoors = new();

    /// <summary>最後に触ったドア情報を更新する（EventHandler の DoorGet から呼ぶ）。</summary>
    public static void UpdateDoor(Player player, DoorInfo info)
        => _lastDoors[player.Id] = info;

    /// <summary>保持しているドア情報を取得する。</summary>
    public static bool TryGetDoor(Player player, out DoorInfo info)
        => _lastDoors.TryGetValue(player.Id, out info);

    public static void SetDebugMode(Player player, bool isOn)
    {
        if (isOn) DebugModePlayers.Add(player);
        else DebugModePlayers.Remove(player);
    }

    // =====================
    //  クリーンアップ
    // =====================

    /// <summary>プレイヤー退出時にエントリを一括掃除する。</summary>
    public static void RemovePlayer(Player player)
    {
        DebugModePlayers.Remove(player);
        _lastDoors.Remove(player.Id);
    }

    public static void ClearAll()
    {
        DebugModePlayers.Clear();
        _lastDoors.Clear();
    }
}
