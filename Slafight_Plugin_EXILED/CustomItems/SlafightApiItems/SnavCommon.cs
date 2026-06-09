using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

/// <summary>
/// SNAV 系 (300/310/Ultimate) で共有する範囲計算とヒント文字列。
/// 元実装でもコピペで散らばっていた処理を 1 箇所にまとめた。
/// </summary>
internal static class SnavCommon
{
    public static float Range(RadioRange mode) => mode switch
    {
        RadioRange.Short  => 30f,
        RadioRange.Medium => 60f,
        RadioRange.Long   => 80f,
        RadioRange.Ultra  => 100f,
        _                 => 0f,
    };

    public static string RangeHint(RadioRange mode) => mode switch
    {
        RadioRange.Short  => "近距離(30m)探知モード",
        RadioRange.Medium => "中距離(60m)探知モード",
        RadioRange.Long   => "長距離(80m)探知モード",
        RadioRange.Ultra  => "超長距離(100m)探知モード",
        _                 => string.Empty,
    };

    public static float Consumption(RadioRange mode) => mode switch
    {
        RadioRange.Short  => 5f,
        RadioRange.Medium => 10f,
        RadioRange.Long   => 15f,
        RadioRange.Ultra  => 25f,
        _                 => 40f,
    };

    public static List<Room> DetectRooms(Vector3 origin, RadioRange mode, IReadOnlyCollection<RoomType> targets)
    {
        var range = Range(mode);
        if (range <= 0f) return [];

        return Room.List
            .Where(r => r != null && targets.Contains(r.Type) && Vector3.Distance(origin, r.Position) <= range)
            .OrderBy(r => Vector3.Distance(origin, r.Position))
            .ToList();
    }

    public static string RoomsHint(RadioRange mode, List<Room> detected, Vector3 origin)
        => detected.Count > 0
            ? $"[{mode}]見つかった部屋：\n" + string.Join("\n", detected.Select(r => $"{r.Type.TranslateRoomName()}: {Vector3.Distance(origin, r.Position):F0}m"))
            : "検知された部屋なし";
}
