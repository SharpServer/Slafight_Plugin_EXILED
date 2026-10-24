using System;
using System.Collections.Generic;

namespace Slafight_Plugin_EXILED.API.Core.Extensions;

/// <summary>
/// 新API層が使うコレクション補助です。
/// </summary>
public static class CollectionExtensions
{
    private static readonly Random Random = new Random();
    private static readonly object RandomLock = new object();

    /// <summary>
    /// List・配列など、IList をその場でシャッフルします (Fisher-Yates)。
    /// </summary>
    public static void Shuffle<T>(this IList<T> list)
    {
        if (list is null)
            throw new ArgumentNullException(nameof(list));

        lock (RandomLock)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int randomIndex = Random.Next(i + 1);

                (list[i], list[randomIndex]) = (list[randomIndex], list[i]);
            }
        }
    }

    /// <summary>
    /// 指定した <see cref="Random"/> を使ってその場でシャッフルします。
    /// シードを固定したい場合に使います。
    /// </summary>
    public static void Shuffle<T>(this IList<T> list, Random random)
    {
        if (list is null)
            throw new ArgumentNullException(nameof(list));

        if (random is null)
            throw new ArgumentNullException(nameof(random));

        for (int i = list.Count - 1; i > 0; i--)
        {
            int randomIndex = random.Next(i + 1);

            (list[i], list[randomIndex]) = (list[randomIndex], list[i]);
        }
    }

    /// <summary>
    /// 元のコレクションを変更せず、シャッフル済みの List を返します。
    /// </summary>
    public static List<T> Shuffled<T>(this IEnumerable<T> source)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));

        List<T> result = [.. source];
        result.Shuffle();

        return result;
    }

    /// <summary>
    /// 指定した <see cref="Random"/> を使い、シャッフル済みの List を返します。
    /// </summary>
    public static List<T> Shuffled<T>(this IEnumerable<T> source, Random random)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));

        if (random is null)
            throw new ArgumentNullException(nameof(random));

        List<T> result = [.. source];
        result.Shuffle(random);

        return result;
    }
}
