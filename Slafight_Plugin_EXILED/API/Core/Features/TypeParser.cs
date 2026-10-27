#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Features.Attributes;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// 文字列 (RA コマンド・マップ・JSON) から型を解決します。結果は <see cref="Type.FullName"/> の序数順です。
/// </summary>
public static class TypeParser
{
    /// <summary>
    /// 文字列から <typeparamref name="TBase"/> の派生型を取得します。型名・完全修飾名のどちらでも検索できます。
    /// </summary>
    /// <remarks>型名が衝突していれば失敗します。見つからなければ <see cref="LegacyNameAttribute"/> も探します。</remarks>
    public static bool TryParse<TBase>(string value, out Type? result, bool ignoreCase = true)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        StringComparison comparison = ignoreCase
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        List<Type> concrete = [.. Concrete<TBase>()];

        List<Type> candidates =
        [
            .. concrete
                .Where(type =>
                    string.Equals(type.Name, value, comparison) ||
                    string.Equals(type.FullName, value, comparison))
        ];

        // 現在の名前が優先。昔の名前を先に見ると、改名後の型に旧名が奪われる。
        if (candidates.Count == 0)
        {
            candidates =
            [
                .. concrete
                    .Where(type => HasLegacyName(type, value, comparison))
            ];
        }

        if (candidates.Count == 1)
        {
            result = candidates[0];

            return true;
        }

        if (candidates.Count == 0)
            return false;

        // 完全修飾名での一致が 1 つだけなら、それが指名されたものとみなす。
        List<Type> exact =
        [
            .. candidates
                .Where(type => string.Equals(type.FullName, value, comparison))
        ];

        if (exact.Count == 1)
        {
            result = exact[0];

            return true;
        }

        Log.Error(
            $"[Slafight] '{value}' が複数の型に一致するため解決できません: " +
            $"{string.Join(", ", candidates.Select(type => type.FullName))}");

        return false;
    }

    /// <summary>
    /// 文字列から、<typeparamref name="TBase"/> を継承しているクラスのインスタンスを生成します。
    /// 引数なしコンストラクターが必要です。
    /// </summary>
    public static bool TryCreate<TBase>(string value, out TBase? instance, bool ignoreCase = true)
    {
        instance = default;

        if (!TryParse<TBase>(value, out Type? type, ignoreCase))
            return false;

        try
        {
            instance = (TBase?)Activator.CreateInstance(type!);

            return instance is not null;
        }
        catch (Exception exception)
        {
            Log.Error($"[Slafight] {type!.FullName} のインスタンス生成に失敗しました: {exception}");

            return false;
        }
    }

    /// <summary>
    /// <typeparamref name="TBase"/> を継承し、引数なしコンストラクターを持つ具象型をすべて返します。
    /// </summary>
    public static IReadOnlyList<Type> FindTypes<TBase>()
    {
        return
        [
            .. Concrete<TBase>()
                .Where(type =>
                    !type.IsGenericTypeDefinition &&
                    type.GetConstructor(Type.EmptyTypes) is not null)
        ];
    }

    /// <summary>
    /// <typeparamref name="TBase"/> を継承している具象型を、コンストラクタの有無を問わず返します。
    /// 「継承しているのに登録されていない」型を見つける用途に使います。
    /// </summary>
    public static IReadOnlyList<Type> FindAllTypes<TBase>() => [.. Concrete<TBase>()];

    /// <summary>
    /// <typeparamref name="TBase"/> を継承している具象型を、決定的な順序で列挙します。
    /// </summary>
    private static IEnumerable<Type> Concrete<TBase>()
    {
        return AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(GetLoadableTypes)
            .Where(type =>
                typeof(TBase).IsAssignableFrom(type) &&
                type != typeof(TBase) &&
                !type.IsAbstract &&
                !type.IsInterface)
            .OrderBy(type => type.FullName, StringComparer.Ordinal);
    }

    /// <summary>
    /// この型が <paramref name="value"/> という名前を昔名乗っていたか。
    /// </summary>
    private static bool HasLegacyName(Type type, string value, StringComparison comparison)
    {
        foreach (LegacyNameAttribute legacy in type.GetCustomAttributes<LegacyNameAttribute>(inherit: false))
        {
            if (string.Equals(legacy.Name, value, comparison))
                return true;
        }

        return false;
    }

    private static Type[] GetLoadableTypes(Assembly assembly)
    {
        if (assembly.IsDynamic)
            return [];

        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return
            [
                .. exception.Types
                    .Where(type => type is not null)
                    .Cast<Type>()
            ];
        }
    }
}
