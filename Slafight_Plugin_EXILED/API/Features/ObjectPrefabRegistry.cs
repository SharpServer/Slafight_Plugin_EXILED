using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// ObjectPrefab 派生型のキャッシュ済みレジストリ。
/// 文字列（クラス名 / 完全名 / 一意な部分一致）からの型解決と生成を一元化する。
/// Bridge / Loader / DevTools はすべてここを経由する。
/// </summary>
public static class ObjectPrefabRegistry
{
    private static Dictionary<string, Type>? _byName;
    private static List<Type>? _all;

    /// <summary>
    /// 登録済みのすべての ObjectPrefab 具象型（クラス名順）。
    /// </summary>
    public static IReadOnlyList<Type> All
    {
        get
        {
            EnsureInitialized();
            return _all!;
        }
    }

    /// <summary>
    /// 名前から ObjectPrefab 型を解決する。
    /// 完全名・クラス名・名前空間サフィックスの完全一致を優先し、
    /// 見つからなければ一意な部分一致を許可する。
    /// </summary>
    public static bool TryResolve(string input, out Type prefabType, out string error)
    {
        EnsureInitialized();
        prefabType = null!;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Prefab class name is empty.";
            return false;
        }

        string trimmed = input.Trim();
        if (_byName!.TryGetValue(trimmed, out prefabType))
            return true;

        var matches = _all!
            .Where(t => t.Name.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();

        if (matches.Count == 1)
        {
            prefabType = matches[0];
            return true;
        }

        error = matches.Count > 1
            ? $"Prefab class '{input}' is ambiguous: {string.Join(", ", matches.Select(t => t.Name))}"
            : $"Prefab class '{input}' not found.";
        return false;
    }

    /// <summary>
    /// 名前から解決してインスタンスを生成する。Create() は呼ばない。
    /// </summary>
    public static bool TryCreateInstance(string input, out ObjectPrefab prefab, out string error)
    {
        prefab = null!;
        if (!TryResolve(input, out Type type, out error))
            return false;

        prefab = (ObjectPrefab)Activator.CreateInstance(type)!;
        return true;
    }

    private static void EnsureInitialized()
    {
        if (_byName != null)
            return;

        var all = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(ObjectPrefab)))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

        var byName = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        foreach (Type type in all)
        {
            byName[type.Name] = type;
            if (type.FullName != null)
                byName[type.FullName] = type;
        }

        _all = all;
        _byName = byName;
    }
}
