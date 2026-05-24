using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using Exiled.CustomItems.API.Features;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;
using Random = System.Random;

namespace Slafight_Plugin_EXILED.Extensions;

public static class StaticUtils
{
    private const float ItemPickupRadius = 1.05f;
    private const float ItemPickupRadiusSqr = ItemPickupRadius * ItemPickupRadius;

    private static readonly Random _random = new();

    // ───────────────────────────────
    // Player 選出
    // ───────────────────────────────

    /// <summary>
    /// 指定のCTeamだけ/以外のPlayer選出リストを作成します。
    /// </summary>
    public static List<Player> CandidatePlayersByCTeam(CTeam team, bool elseMode = false)
    {
        var allPlayers = Player.List.ToList();
        return elseMode
            ? allPlayers.Where(p => p != null && p.GetTeam() != team).ToList()
            : allPlayers.Where(p => p != null && p.GetTeam() == team).ToList();
    }

    /// <summary>
    /// 指定のCTeam選出リストをランダムにシャッフルしてからreturnします。
    /// </summary>
    public static List<Player> SelectRandomPlayersByRatio(CTeam excludeTeam, float ratio, bool elseMode = false)
    {
        var allPlayers = Player.List.ToList();
        if (allPlayers.Count == 0) return [];

        var candidates = CandidatePlayersByCTeam(excludeTeam, elseMode);
        int targetCount = Math.Min((int)Math.Truncate(allPlayers.Count * ratio), candidates.Count);

        return candidates.ShuffleTake(targetCount).ToList();
    }

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

    public static void GiveOrDrop(this Player player, uint customId)
    {
        if (player.IsInventoryFull)
            CustomItem.TrySpawn(customId, player.Position + Vector3.up * 0.5f, out _);
        else
            player.TryAddCustomItem(customId);
    }

    /// <summary>型引数でカスタムアイテムを付与 or ドロップします。</summary>
    public static void GiveOrDrop<T>(this Player player) where T : CustomItem
    {
        if (!CustomItemExtensions.TryGet<T>(out var item) || item is null) return;

        if (player.IsInventoryFull)
            CustomItem.TrySpawn(item.Id, player.Position + Vector3.up * 0.5f, out _);
        else
            player.TryAddCustomItem<T>();
    }
    
    /// <summary>型引数でCItemを付与 or ドロップします。</summary>
    public static void GiveOrDrop<T>(this Player player, bool showHint = false) where T : CItem
    {
        if (player.IsInventoryFull)
            CItem.Get<T>()?.Spawn(player.Position + Vector3.up * 0.5f);
        else
            CItem.Get<T>()?.Give(player, showHint);
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
                if (keycard.Permissions.HasFlagFast(permissions)) return true;
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

    public static bool HasWornGoggle<T>(this Player player) where T : CustomGoggles
    {
        return player.Items
            .OfType<Scp1344>()
            .Any(i => i.TryGetCustomItem(out var ci) && ci is T && i.IsWorn);
    }

    // ───────────────────────────────
    // SaveItems (Player 拡張)
    // ───────────────────────────────

    public static void SaveItems(this Player player)
    {
        var nowPos = player.Position;
        player.DropItems();

        var saveItems = Pickup.List
            .Where(p => p != null && p.PreviousOwner == player && (p.Position - nowPos).sqrMagnitude <= ItemPickupRadiusSqr)
            .ToList();

        if (saveItems.Count == 0) return;

        Timing.CallDelayed(0.5f, () =>
        {
            if (player?.IsConnected != true) return;
            var newPos = player.Position + new Vector3(0f, 0.15f, 0f);
            foreach (var item in saveItems)
                if (item?.IsSpawned == true) item.Position = newPos;
        });
    }

    // ───────────────────────────────
    // チーム判定 (Player? 拡張)
    // ───────────────────────────────

    public static bool IsFifthist(this Player? player)
    {
        if (player == null) return false;
        return player.GetTeam() == CTeam.Fifthists || player.GetCustomRole() == CRoleTypeId.Scp3005;
    }

    public static bool IsHumanitist(this Player? player)
    {
        if (player == null) return false;
        return player.GetTeam() != CTeam.FoundationForces && player.GetTeam() != CTeam.Guards;
    }

    public static bool IsCandyWarrier(this Player? player)
    {
        if (player == null) return false;
        return player.GetCustomRole() is CRoleTypeId.CandyWarrierApril or CRoleTypeId.CandyWarrierHalloween;
    }

    public static uint GetNetId(this Player? player)
    {
        if (player == null || player.ReferenceHub == null) return 0;
        return player.NetId;
    }

    public static bool IsVanillaOrCustom(this Player? player, RoleTypeId roleTypeId, CRoleTypeId cRoleTypeId)
    {
        if (player == null) return false;
        if (player.GetCustomRole() == CRoleTypeId.None && player.Role.Type == roleTypeId) return true;
        if (player.GetCustomRole() == cRoleTypeId) return true;
        return false;
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

    public static bool IsValid(Player? player, CRoleTypeId roleId) =>
        player != null &&
        player.IsAlive &&
        player.GetCustomRole() == roleId &&
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
    
    public static void CreateAndPlayAudio(string fileName, string audioPlayerName, Vector3 position, bool destroyOnEnd = false, Transform parent = null, bool isSpatial = false, float maxDistance = 5, float minDistance = 5)
    {
        var audioPlayer = AudioPlayer.CreateOrGet(audioPlayerName);

        if (!audioPlayer.TryGetSpeaker(audioPlayerName, out var speaker))
        {
            speaker = audioPlayer.AddSpeaker(audioPlayerName, isSpatial: isSpatial, maxDistance: maxDistance, minDistance: minDistance);
        }

        if (parent)
        {
            speaker.transform.SetParent(parent);
            speaker.transform.localPosition = Vector3.zero;
            speaker.transform.localRotation = Quaternion.identity;
        }
        else
        {
            speaker.Position = position;
        }

        AudioClipStorage.LoadClip(Path.Combine(Plugin.Singleton.Config.AudioReferences, fileName), fileName);
        audioPlayer.AddClip(fileName, destroyOnEnd: destroyOnEnd);
    }

    public static void PlayKeycardInteractSound(this Player? player, bool isSuccess)
    {
        if (player is null) return;
        SpeakerApi.Play(isSuccess ? "KeycardUse1.ogg" : "KeycardUse2.ogg", "KeycardDoor", player.Position, true);
    }
}