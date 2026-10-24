#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Features.Attributes;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// 型そのものを同一性として扱うための型解決です。
///
/// 外部から文字列で指す必要がある箇所 (RA コマンド・ProjectMER のマップ・保存済み JSON) は
/// ここが型名で解決します。<b>各クラスに <c>UniqueKey</c> プロパティを生やさないでください。</b>
/// </summary>
/// <remarks>
/// 結果は <see cref="Type.FullName"/> の序数順で返します。
/// 既存の <c>API/Features/Common/DefinitionSourceLoader</c> が同じ順序付けをしており、
/// <see cref="AppDomain.GetAssemblies"/> の並び順に依存して挙動が変わるのを防ぎます。
///
/// ここには結果のキャッシュを置きません。全件が要る利用側 (チーム一覧など) が
/// 自分で一度だけ組み立てて持ちます。
/// </remarks>
public static class TypeParser
{
    /// <summary>
    /// 文字列から、<typeparamref name="TBase"/> を継承している型を取得します。
    /// 型名・完全修飾名のどちらでも検索できます。
    /// </summary>
    /// <remarks>
    /// 型名が複数の名前空間で衝突している場合は、<b>どれか 1 つを勝手に選ばず失敗させます</b>。
    /// 黙って片方に解決すると、コマンドやマップデータの指す先が不定になるためです。
    /// その場合は完全修飾名を渡してください。
    ///
    /// 現在の名前で見つからなかったときだけ、<see cref="LegacyNameAttribute"/> が
    /// 名乗っている昔の綴りも探します。再構築前に作られたマップやセーブが指す先を拾うためです。
    /// </remarks>
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
    /// <typeparamref name="TBase"/> を継承している具象型をすべて返します。
    /// 「宣言されているものを全部集める」用途 (チーム一覧・カタログ表示など) に使います。
    /// 引数なしコンストラクターを持つ型だけが対象です。
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
