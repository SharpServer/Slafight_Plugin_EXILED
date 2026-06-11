using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// UnitPack を一元管理するレジストリ。
/// どこからでも部隊プリセットを参照・登録できる。
/// </summary>
public static class UnitPackRegistry
{
    private static readonly Dictionary<string, UnitPack> PacksByName = new();
    // SpawnTypeId からの逆引き用（必要なら）
    private static readonly Dictionary<SpawnTypeId, List<UnitPack>> PacksBySpawnType = new();

    public static void Register(UnitPack? pack)
    {
        if (pack == null || string.IsNullOrWhiteSpace(pack.Name))
        {
            Log.Warn("UnitPackRegistry: Tried to register null or unnamed pack.");
            return;
        }

        PacksByName[pack.Name] = pack;

        foreach (var spawnType in pack.RoleTables.Keys)
        {
            if (!PacksBySpawnType.TryGetValue(spawnType, out var list))
            {
                list = [];
                PacksBySpawnType[spawnType] = list;
            }

            if (!list.Contains(pack))
                list.Add(pack);
        }

        Log.Info($"UnitPackRegistry: Pack '{pack.Name}' registered.");
    }

    public static bool Unregister(string name)
    {
        if (!PacksByName.TryGetValue(name, out var pack))
            return false;

        PacksByName.Remove(name);

        foreach (var kvp in PacksBySpawnType.ToList())
        {
            kvp.Value.Remove(pack);
            if (kvp.Value.Count == 0)
                PacksBySpawnType.Remove(kvp.Key);
        }

        Log.Info($"UnitPackRegistry: Pack '{name}' unregistered.");
        return true;
    }

    public static bool TryGet(string name, out UnitPack pack)
        => PacksByName.TryGetValue(name, out pack);

    public static IEnumerable<UnitPack> GetAll()
        => PacksByName.Values;

    public static IEnumerable<UnitPack> GetBySpawnType(SpawnTypeId spawnType)
    {
        if (PacksBySpawnType.TryGetValue(spawnType, out var list))
            return list;

        return [];
    }

    public static void Clear()
    {
        PacksByName.Clear();
        PacksBySpawnType.Clear();
    }
}