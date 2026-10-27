using System;
using System.Collections.Generic;
using System.Linq;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.API.Core.Commands;

/// <summary>
/// 宣言されている型をクラス名で引く共通処理です。見つからなければ候補を返します。
/// </summary>
internal static class CoreCatalog
{
    /// <summary>
    /// 宣言されている <typeparamref name="TBase"/> の具象型を、名前順で返します。
    /// </summary>
    internal static IReadOnlyList<Type> Types<TBase>() =>
    [
        .. TypeParser.FindTypes<TBase>()
            .OrderBy(type => type.Name, StringComparer.Ordinal)
    ];

    /// <summary>
    /// 名前から型を引きます。引けなかったときは候補付きの理由を返します。
    /// </summary>
    internal static bool TryResolve<TBase>(string name, out Type type, out string failure)
    {
        if (TypeParser.TryParse<TBase>(name, out type))
        {
            failure = null;

            return true;
        }

        failure = $"'{name}' は見つかりません。\n{Names<TBase>()}";

        return false;
    }

    /// <summary>
    /// 宣言されている名前を 1 行に並べます。
    /// </summary>
    internal static string Names<TBase>()
    {
        IReadOnlyList<Type> types = Types<TBase>();

        if (types.Count == 0)
            return "  (宣言されているものがありません)";

        return "  " + string.Join(", ", types.Select(type => type.Name));
    }

    /// <summary>
    /// 一覧の各行に付ける見出しです。
    /// </summary>
    internal static string Header(string title, int count) => $"<b>{title}</b>  ({count})";

    /// <summary>
    /// 絞り込み文字列に一致するか。空なら全部通します。
    /// </summary>
    internal static bool Matches(Type type, string filter) =>
        string.IsNullOrWhiteSpace(filter) ||
        type.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;

    /// <summary>
    /// 一覧表示用に型からインスタンスを 1 つ作ります。作れなければ null。
    /// </summary>
    internal static T Probe<T>(Type type) where T : class
    {
        try
        {
            return Activator.CreateInstance(type) as T;
        }
        catch
        {
            return null;
        }
    }
}
