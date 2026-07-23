using System.Collections.Generic;
using Exiled.API.Features;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// プレイヤーが操作しない演出・武装・チーム表現用の内部 Npc を種別ごとに一元管理する。
/// 勝利判定・生存者集計・観戦保護などから除外する対象かどうかを、この登録状況だけで判定できるようにする。
/// </summary>
public enum InternalNpcCategory
{
    Generic,
    HidTurret,
    Tentacle,
    TeamNpc,
}

public static class InternalNpcRegistry
{
    private static readonly Dictionary<int, InternalNpcCategory> ManagedNpcs = new();

    public static void Register(Npc? npc, InternalNpcCategory category)
    {
        if (npc == null)
            return;

        ManagedNpcs[npc.Id] = category;
    }

    public static void Unregister(int npcId)
    {
        ManagedNpcs.Remove(npcId);
    }

    public static bool IsManaged(int playerId)
    {
        return ManagedNpcs.ContainsKey(playerId);
    }

    public static bool IsCategory(int playerId, InternalNpcCategory category)
    {
        return ManagedNpcs.TryGetValue(playerId, out var registered) && registered == category;
    }

    public static void Clear()
    {
        ManagedNpcs.Clear();
    }
}
