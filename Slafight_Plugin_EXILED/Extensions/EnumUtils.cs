using System;
using System.Linq;

namespace Slafight_Plugin_EXILED.Extensions;

public static class EnumUtils
{
    private static readonly Random Random = new();
    private static readonly object RandomLock = new();

    public static T GetRandom<T>() where T : struct, Enum
    {
        T[] values = EnumCache<T>.Values;

        if (values.Length == 0)
            throw new InvalidOperationException($"{typeof(T).Name} has no values.");

        lock (RandomLock)
        {
            return values[Random.Next(values.Length)];
        }
    }

    public static T[] GetValues<T>() where T : struct, Enum
    {
        return EnumCache<T>.Values;
    }

    private static class EnumCache<T> where T : struct, Enum
    {
        public static readonly T[] Values = Enum.GetValues(typeof(T))
            .Cast<T>()
            .ToArray();
    }
}