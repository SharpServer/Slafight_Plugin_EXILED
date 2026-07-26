using System;
using System.Collections.Generic;

namespace Slafight_Plugin_EXILED.Extensions;

public static class CollectionExtensions
{
    public static T GetLoopAt<T>(this IList<T> list, int index)
    {
        if (list.Count == 0)
            throw new ArgumentException("list is empty");

        index %= list.Count;
        if (index < 0)
            index += list.Count;

        return list[index];
    }
}