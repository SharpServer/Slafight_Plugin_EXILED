using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using MEC;
using Slafight_Plugin_EXILED.API.Enums;
using Utils.NonAllocLINQ;

namespace Slafight_Plugin_EXILED.API.Features;

public static class SpecificFlagsManager
{
    public static Dictionary<int, List<CoroutineHandle>> Coroutines = new();
    public static Dictionary<int, List<SpecificFlagType>> Flags = new();

    // ===== 基本Init =====

    public static void InitPlayerFlags(this Player player)
    {
        if (player == null) return;
        Flags[player.Id] = [];
        KillAllCoroutines(player.Id);
        Coroutines[player.Id] = [];
    }

    /// <summary>
    /// Flags/Coroutines が未初期化なら確実に作る。
    /// </summary>
    private static void EnsurePlayerInitialized(Player? player)
    {
        if (player == null) return;

        if (!Flags.ContainsKey(player.Id))
            Flags[player.Id] = [];

        if (!Coroutines.ContainsKey(player.Id))
            Coroutines[player.Id] = [];
    }

    // ===== 取得系 =====

    public static List<SpecificFlagType>? Get(this Player? player)
    {
        if (player == null) return null;
        if (!Flags.TryGetValue(player.Id, out var list))
            return null;
        return list;
    }

    /// <summary>
    /// プレイヤーが指定フラグを持っているかを安全に確認。
    /// player が null / 未初期化 / Clear 済みでも false。
    /// </summary>
    public static bool HasFlag(this Player? player, SpecificFlagType flag)
    {
        if (player == null)
            return false;

        if (!Flags.TryGetValue(player.Id, out var flags) || flags == null || flags.Count == 0)
            return false;

        return flags.Contains(flag);
    }

    // ===== クリア系 =====

    public static bool Clear(this Player? player)
    {
        try
        {
            if (player == null) return false;

            Flags.Remove(player.Id);
            KillAllCoroutines(player.Id);
            Coroutines.Remove(player.Id);
            return true;
        }
        catch (Exception e)
        {
            Log.Warn($"[SpecificFlagsManager]{player} flags failed to clear: {e.Message}");
            return false;
        }
    }

    public static void ClearAll()
    {
        var keys = Flags.Keys.ToList();
        foreach (int key in keys)
        {
            Flags.Remove(key);
            KillAllCoroutines(key);
            Coroutines.Remove(key);
        }
    }

    // ===== 変更系 =====

    public static bool TryAddFlag(this Player? player, SpecificFlagType flag)
    {
        if (player == null) return false;

        EnsurePlayerInitialized(player);

        var flags = Flags[player.Id];
        return flags.AddIfNotContains(flag);
    }

    public static bool TryRemoveFlag(this Player? player, SpecificFlagType flag)
    {
        if (player == null) return false;

        if (!Flags.TryGetValue(player.Id, out var flags) || flags == null)
            return false;

        return flags.RemoveAll(f => f == flag) > 0;
    }

    public static void WaitAndRemove(this Player? player, SpecificFlagType flag, float time)
    {
        if (player == null) return;

        EnsurePlayerInitialized(player);

        var handle = Timing.RunCoroutine(RemoveCoroutine(player, flag, time));
        Coroutines[player.Id].Add(handle);
    }

    private static IEnumerator<float> RemoveCoroutine(Player player, SpecificFlagType flag, float time)
    {
        if (!Round.InProgress) yield break;

        yield return Timing.WaitForSeconds(time);

        if (player?.IsAlive != true || !Round.InProgress) yield break;

        player.TryRemoveFlag(flag);

        if (Coroutines.TryGetValue(player.Id, out var list))
            list.RemoveAll(h => !h.IsValid);
    }

    // ===== コルーチン管理 =====

    private static void KillAllCoroutines(int playerId)
    {
        if (!Coroutines.TryGetValue(playerId, out var handles)) return;

        foreach (var handle in handles.ToArray())
            Timing.KillCoroutines(handle);

        handles.Clear();
    }
}
