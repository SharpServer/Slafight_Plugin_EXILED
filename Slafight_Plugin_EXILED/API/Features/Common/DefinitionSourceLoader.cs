using System;
using System.Collections.Generic;
using System.Linq;

namespace Slafight_Plugin_EXILED.API.Features;

internal static class DefinitionSourceLoader
{
    public static IReadOnlyList<T> CreateInstances<T>()
    {
        return typeof(DefinitionSourceLoader).Assembly
            .GetTypes()
            .Where(type => typeof(T).IsAssignableFrom(type) &&
                           !type.IsAbstract &&
                           !type.IsInterface)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .Select(type => (T)Activator.CreateInstance(type))
            .ToList();
    }
}
