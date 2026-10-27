using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using MEC;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;
using Random = System.Random;
using CoreCustomItem = Slafight_Plugin_EXILED.API.Core.Features.CustomItem;

namespace Slafight_Plugin_EXILED.Extensions;

public static class StaticUtils
{
    private const float ItemPickupRadius = 1.05f;
    private const float ItemPickupRadiusSqr = ItemPickupRadius * ItemPickupRadius;

    private static readonly Random _random = new();

    // ───────────────────────────────
    // Player 選出
    // ───────────────────────────────

    // ───────────────────────────────
    // IEnumerable<T> 拡張
    // ───────────────────────────────

    private static IEnumerable<T> ShuffleTake<T>(this IEnumerable<T> source, int count)
    {
        var list = source.ToList();
        int n = list.Count;
        if (count >= n) return list;

        for (int i = 0; i < count; i++)
        {
            int pos = _random.Next(i, n);
            (list[i], list[pos]) = (list[pos], list[i]);
        }
        return list.Take(count);
    }

    public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
    {
        var list = source.ToList();
        int n = list.Count;
        for (int i = 0; i < n - 1; i++)
        {
            int j = UnityEngine.Random.Range(i, n);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }

    // ───────────────────────────────
    // GiveOrDrop (Player 拡張)
    // ───────────────────────────────

    public static void GiveOrDrop(this Player player, ItemType itemType)
    {
        if (player.IsInventoryFull)
            Pickup.CreateAndSpawn(itemType, player.Position + Vector3.up * 0.5f);
        else
            player.AddItem(itemType);
    }

    /// <summary>
    /// 型でカスタムアイテムを渡します。インベントリが満杯なら足元に落とします。
    /// </summary>
    public static CoreCustomItem? GiveOrDrop<T>(this Player? player) where T : CoreCustomItem, new()
    {
        if (player is null) return null;

        return player.IsInventoryFull
            ? CoreCustomItem.Spawn<T>(player.Position + Vector3.up * 0.5f)
            : CoreCustomItem.Give<T>(player);
    }

    /// <summary>
    /// 型を実行時に決めて渡します。コマンドやマップデータのように、
    /// 文字列から型を引いてくる経路で使います。
    /// </summary>
    public static CoreCustomItem? GiveOrDrop(this Player? player, Type type)
    {
        if (player is null || type is null) return null;

        return player.IsInventoryFull
            ? CoreCustomItem.Spawn(type, player.Position + Vector3.up * 0.5f)
            : CoreCustomItem.Give(type, player);
    }

    // ───────────────────────────────
    // HasPermission (Player 拡張)
    // ───────────────────────────────

    public static bool HasPermission(this Player player, KeycardPermissions permissions, bool requireAll = false)
    {
        if (permissions == KeycardPermissions.None) return true;

        foreach (var item in player.Items.ToList())
        {
            if (!item.IsKeycard || item is not Keycard keycard) continue;
            if (requireAll)
            {
                if ((keycard.Permissions & permissions) == permissions) return true;
            }
            else
            {
                if (keycard.Permissions.HasFlag(permissions)) return true;
            }
        }
        return false;
    }

    // ───────────────────────────────
    // Position ユーティリティ (Player 拡張)
    // ───────────────────────────────

    /// <summary>
    /// Playerを中心とした四角形範囲(XZ平面)のランダム位置をY固定で取得
    /// </summary>
    public static Vector3 GetRandomSquarePosition(this Player player, float halfSize, float fixedY = float.NaN)
    {
        Vector3 center = player.Position;
        float y = float.IsNaN(fixedY) ? center.y : fixedY;
        float randomX = UnityEngine.Random.Range(center.x - halfSize, center.x + halfSize);
        float randomZ = UnityEngine.Random.Range(center.z - halfSize, center.z + halfSize);
        return new Vector3(randomX, y, randomZ);
    }
    
    public static Vector3 GetRandomSquarePosition(this Vector3 pos, float halfSize, float fixedY = float.NaN)
    {
        Vector3 center = pos;
        float y = float.IsNaN(fixedY) ? center.y : fixedY;
        float randomX = UnityEngine.Random.Range(center.x - halfSize, center.x + halfSize);
        float randomZ = UnityEngine.Random.Range(center.z - halfSize, center.z + halfSize);
        return new Vector3(randomX, y, randomZ);
    }

    // ───────────────────────────────
    // カスタムアイテム確認 (Player 拡張)
    // ───────────────────────────────

    // ───────────────────────────────
    // SaveItems (Player 拡張)
    // ───────────────────────────────

    public static void SaveItems(this Player player)
    {
        var playerId = player.Id;
        var nowPos = player.Position;
        player.DropItems();

        var saveItems = Pickup.List
            .Where(p => p != null && p.PreviousOwner == player && (p.Position - nowPos).sqrMagnitude <= ItemPickupRadiusSqr)
            .ToList();

        if (saveItems.Count == 0) return;

        Timing.CallDelayed(0.5f, () =>
        {
            var currentPlayer = Player.Get(playerId);
            if (currentPlayer?.ReferenceHub == null) return;

            var newPos = currentPlayer.Position + new Vector3(0f, 0.15f, 0f);
            foreach (var item in saveItems)
                if (item?.IsSpawned == true) item.Position = newPos;
        });
    }

    // ───────────────────────────────
    // チーム判定 (Player? 拡張)
    // ───────────────────────────────

    public static uint GetNetId(this Player? player)
    {
        if (player == null || player.ReferenceHub == null) return 0;
        return player.NetId;
    }

    // ───────────────────────────────
    // ラウンドユーティリティ
    // ───────────────────────────────

    public static void TryRestart()
    {
        if (!Round.InProgress || Round.IsLobby || !Round.IsStarted || RoundSummary.SummaryActive) return;
        Round.Restart(false);
    }

    public static bool IsValid(Player? player) =>
        player != null &&
        player.IsAlive &&
        Round.InProgress;

    // ───────────────────────────────
    // 部屋座標変換
    // ───────────────────────────────

    /// <summary>
    /// 指定した RoomType の部屋のローカル座標・ローカル回転から、
    /// ワールド座標・ワールド回転を計算して返します。
    /// </summary>
    public static (Vector3 worldPosition, Quaternion worldRotation) GetWorldFromRoomLocal(
        RoomType roomType,
        Vector3 localPosition,
        Vector3 localEulerAngles)
    {
        var room = Room.List.FirstOrDefault(r => r.Type == roomType);
        if (room == null)
        {
            Quaternion localRotOnly = Quaternion.Euler(localEulerAngles);
            return (localPosition, localRotOnly);
        }

        Quaternion roomRot = room.Rotation;
        Vector3 worldPos = room.Position + roomRot * localPosition;
        Quaternion worldRot = roomRot * Quaternion.Euler(localEulerAngles);

        return (worldPos, worldRot);
    }

    /// <summary>
    /// ワールド座標・回転から、指定した RoomType の部屋ローカル座標・ローカル回転を計算します。
    /// </summary>
    public static (Vector3 localPosition, Vector3 localEulerAngles) GetRoomLocalFromWorld(
        RoomType roomType,
        Vector3 worldPosition,
        Quaternion worldRotation)
    {
        var room = Room.List.FirstOrDefault(r => r.Type == roomType);
        if (room == null)
        {
            Log.Warn($"[RoomSpaceUtility] RoomType {roomType} not found. Returning zero local.");
            return (Vector3.zero, Vector3.zero);
        }

        Quaternion invRoomRot = Quaternion.Inverse(room.Rotation);
        Vector3 localPos = invRoomRot * (worldPosition - room.Position);
        Quaternion localRot = invRoomRot * worldRotation;

        return (localPos, localRot.eulerAngles);
    }
    
    public static void LogHierarchy(Transform parent, int level)
    {
        string indent = new string(' ', level * 2);  // インデント作成
        Log.Debug($"{indent + parent.name}, {parent.gameObject}");

        for (int i = 0; i < parent.childCount; i++)
        {
            LogHierarchy(parent.GetChild(i), level + 1);
        }
    }
    
    public static void PlayKeycardInteractSound(this Player? player, bool isSuccess)
    {
        if (player is null) return;
        SpeakerApi.Play(isSuccess ? "KeycardUse1.ogg" : "KeycardUse2.ogg", "KeycardDoor", player.Position, true);
    }
    
    public static Color32 ToGradientColor(float value, bool redToGreen = false)
    {
        value = Mathf.Clamp01(value);

        byte red = (byte)(redToGreen ? Mathf.RoundToInt(255 * (1f - value)) : 0);
        byte green = (byte)Mathf.RoundToInt(255 * value);
        byte blue = (byte)(redToGreen ? 0 : Mathf.RoundToInt(255 * (1f - value)));

        return new Color32(red, green, blue, 255);
    }
    
    /// <summary>
    /// <paramref name="colorA"/> から <paramref name="colorB"/> へ <paramref name="progress"/> (0〜1) で補間した色を返します。
    /// </summary>
    public static Color GetGradientColor(Color colorA, Color colorB, float progress)
    {
        // 進行度合いを 0.0〜1.0 の範囲に制限
        progress = Mathf.Clamp(progress, 0f, 1f);
        
        // 各チャンネルを線形補間
        return new Color(
            Mathf.Lerp(colorA.r, colorB.r, progress),
            Mathf.Lerp(colorA.g, colorB.g, progress),
            Mathf.Lerp(colorA.b, colorB.b, progress),
            Mathf.Lerp(colorA.a, colorB.a, progress)
        );
    }
    
    /// <summary>
    /// Unity の Color.Lerp を使用した簡易版
    /// </summary>
    public static Color GetGradientColorSimple(Color colorA, Color colorB, float progress)
    {
        return Color.Lerp(colorA, colorB, Mathf.Clamp(progress, 0f, 1f));
    }
    
    public static Vector3 GetScale(Vector3 a, Vector3 b, float elapsed, float duration)
    {
        float t = Mathf.Clamp01(elapsed / duration);
        return Vector3.Lerp(a, b, t);
    }
}
