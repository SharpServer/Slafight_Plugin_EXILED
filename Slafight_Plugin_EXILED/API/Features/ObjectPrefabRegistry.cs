using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Exiled.API.Features;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// Immutable metadata for one ObjectPrefab registration.
/// </summary>
public sealed class ObjectPrefabDescriptor
{
    private readonly Func<ObjectPrefab> _constructor;

    internal ObjectPrefabDescriptor(
        string key,
        string displayName,
        Type prefabType,
        IEnumerable<string> aliases,
        Func<ObjectPrefab> constructor)
    {
        Key = key;
        DisplayName = displayName;
        PrefabType = prefabType;
        Aliases = Array.AsReadOnly(aliases
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Select(alias => alias.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray());
        _constructor = constructor;
    }

    /// <summary>Stable external registration key used by persistence and bridges.</summary>
    public string Key { get; }

    /// <summary>Human-readable name shown by developer tools.</summary>
    public string DisplayName { get; }

    /// <summary>Concrete ObjectPrefab type represented by this descriptor.</summary>
    public Type PrefabType { get; }

    /// <summary>Additional exact names accepted when resolving the descriptor.</summary>
    public IReadOnlyList<string> Aliases { get; }

    /// <summary>Invoke the cached parameterless constructor.</summary>
    public ObjectPrefab Create() => _constructor();
}

/// <summary>
/// ObjectPrefab descriptor registry. Built-in types are discovered lazily and
/// external integrations may register stable keys explicitly.
/// </summary>
public static class ObjectPrefabRegistry
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, ObjectPrefabDescriptor> ByKey =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, ObjectPrefabDescriptor> ByAlias =
        new(StringComparer.OrdinalIgnoreCase);
    private static bool _initialized;

    /// <summary>All registered descriptors in stable key order.</summary>
    public static IReadOnlyList<ObjectPrefabDescriptor> All
    {
        get
        {
            EnsureInitialized();
            lock (Sync)
                return Array.AsReadOnly(ByKey.Values.OrderBy(descriptor => descriptor.Key, StringComparer.Ordinal).ToArray());
        }
    }

    /// <summary>Registers an ObjectPrefab type with a stable key and optional aliases.</summary>
    public static ObjectPrefabDescriptor Register(
        Type prefabType,
        string key,
        string? displayName = null,
        IEnumerable<string>? aliases = null)
    {
        EnsureInitialized();
        if (prefabType == null)
            throw new ArgumentNullException(nameof(prefabType));
        if (!typeof(ObjectPrefab).IsAssignableFrom(prefabType) || prefabType.IsAbstract)
            throw new ArgumentException($"{prefabType.FullName} is not a concrete ObjectPrefab type.", nameof(prefabType));

        string normalizedKey = NormalizeRequired(key, nameof(key));
        string name = string.IsNullOrWhiteSpace(displayName) ? prefabType.Name : displayName.Trim();
        var names = new List<string> { prefabType.Name };
        if (!string.IsNullOrWhiteSpace(prefabType.FullName))
            names.Add(prefabType.FullName!);
        if (aliases != null)
            names.AddRange(aliases);

        Func<ObjectPrefab> constructor = () =>
        {
            try
            {
                return (ObjectPrefab)Activator.CreateInstance(prefabType)!;
            }
            catch (Exception exception)
            {
                throw new ObjectPrefabCreationException(normalizedKey, prefabType, exception);
            }
        };

        var descriptor = new ObjectPrefabDescriptor(normalizedKey, name, prefabType, names, constructor);
        lock (Sync)
        {
            if (ByKey.ContainsKey(normalizedKey) || ByAlias.ContainsKey(normalizedKey))
                throw new InvalidOperationException($"ObjectPrefab key '{normalizedKey}' is already registered.");

            foreach (string alias in descriptor.Aliases)
            {
                if (ByKey.ContainsKey(alias))
                    throw new InvalidOperationException($"ObjectPrefab alias '{alias}' conflicts with a registered key.");
                if (ByAlias.TryGetValue(alias, out ObjectPrefabDescriptor? existing) &&
                    !ReferenceEquals(existing, descriptor))
                    throw new InvalidOperationException($"ObjectPrefab alias '{alias}' is already registered.");
            }

            ByKey[normalizedKey] = descriptor;
            foreach (string alias in descriptor.Aliases)
                ByAlias[alias] = descriptor;
        }

        return descriptor;
    }

    /// <summary>Registers a concrete ObjectPrefab type using its full name as the key.</summary>
    public static ObjectPrefabDescriptor Register<TPrefab>(
        string key,
        string? displayName = null,
        params string[] aliases)
        where TPrefab : ObjectPrefab
        => Register(typeof(TPrefab), key, displayName, aliases);

    /// <summary>Unregisters a descriptor by its stable key.</summary>
    public static bool Unregister(string key)
    {
        EnsureInitialized();
        if (string.IsNullOrWhiteSpace(key))
            return false;

        lock (Sync)
        {
            if (!ByKey.TryGetValue(key.Trim(), out ObjectPrefabDescriptor? descriptor))
                return false;

            ByKey.Remove(descriptor.Key);
            foreach (string alias in descriptor.Aliases)
                ByAlias.Remove(alias);
            return true;
        }
    }

    /// <summary>Resolves only stable keys, aliases, full names, or class names.</summary>
    public static bool TryResolveExact(string input, out ObjectPrefabDescriptor descriptor, out string error)
        => TryResolveDescriptor(input, out descriptor, out error, allowFuzzy: false);

    /// <summary>
    /// Resolves a descriptor. Fuzzy matching is opt-in and should only be used for user input.
    /// </summary>
    public static bool TryResolveDescriptor(
        string input,
        out ObjectPrefabDescriptor descriptor,
        out string error,
        bool allowFuzzy = false)
    {
        EnsureInitialized();
        descriptor = null!;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Prefab key or class name is empty.";
            return false;
        }

        string trimmed = input.Trim();
        lock (Sync)
        {
            if (ByKey.TryGetValue(trimmed, out descriptor!) || ByAlias.TryGetValue(trimmed, out descriptor!))
                return true;

            if (!allowFuzzy)
            {
                error = $"Prefab '{input}' not found.";
                return false;
            }

            var matches = ByKey.Values
                .Where(candidate => candidate.Key.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    candidate.DisplayName.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    candidate.PrefabType.Name.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0)
                .Distinct()
                .OrderBy(candidate => candidate.Key, StringComparer.Ordinal)
                .ToList();

            if (matches.Count == 1)
            {
                descriptor = matches[0];
                return true;
            }

            error = matches.Count > 1
                ? $"Prefab '{input}' is ambiguous: {string.Join(", ", matches.Select(candidate => candidate.DisplayName))}"
                : $"Prefab '{input}' not found.";
            return false;
        }
    }

    /// <summary>Legacy type-resolution overload. It retains fuzzy matching for old commands.</summary>
    public static bool TryResolve(string input, out Type prefabType, out string error)
    {
        prefabType = null!;
        if (!TryResolveDescriptor(input, out ObjectPrefabDescriptor descriptor, out error, allowFuzzy: true))
            return false;

        prefabType = descriptor.PrefabType;
        return true;
    }

    /// <summary>Creates a prefab without invoking Create(), reporting constructor failures.</summary>
    public static bool TryCreate(
        string input,
        out ObjectPrefab prefab,
        out string error,
        bool allowFuzzy = false)
    {
        prefab = null!;
        if (!TryResolveDescriptor(input, out ObjectPrefabDescriptor descriptor, out error, allowFuzzy))
            return false;

        return TryCreate(descriptor, out prefab, out error);
    }

    /// <summary>Creates a prefab from an already resolved descriptor.</summary>
    public static bool TryCreate(
        ObjectPrefabDescriptor descriptor,
        out ObjectPrefab prefab,
        out string error)
    {
        prefab = null!;
        error = string.Empty;
        try
        {
            prefab = descriptor.Create();
            return prefab != null;
        }
        catch (Exception exception)
        {
            error = exception is ObjectPrefabCreationException creation
                ? creation.Message
                : $"Failed to construct prefab '{descriptor.Key}': {exception}";
            Log.Error($"[ObjectPrefabRegistry] {error}");
            return false;
        }
    }

    /// <summary>Generic convenience API for external callers.</summary>
    public static bool TryCreate<TPrefab>(out TPrefab prefab, out string error)
        where TPrefab : ObjectPrefab
    {
        prefab = null!;
        if (!TryResolveExact(typeof(TPrefab).FullName ?? typeof(TPrefab).Name,
                out ObjectPrefabDescriptor descriptor, out error))
            return false;

        if (!TryCreate(descriptor, out ObjectPrefab created, out error) || created is not TPrefab typed)
        {
            prefab = null!;
            error = string.IsNullOrEmpty(error) ? $"Resolved prefab is not {typeof(TPrefab).Name}." : error;
            return false;
        }

        prefab = typed;
        return true;
    }

    private static void EnsureInitialized()
    {
        lock (Sync)
        {
            if (_initialized)
                return;

            _initialized = true;
            var types = Assembly.GetExecutingAssembly().GetTypes()
                .Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(ObjectPrefab)))
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();

            foreach (Type type in types)
            {
                string key = type.FullName ?? type.Name;
                try
                {
                    RegisterCore(type, key);
                }
                catch (Exception exception)
                {
                    Log.Error($"[ObjectPrefabRegistry] Failed to discover '{type.FullName}': {exception}");
                }
            }
        }
    }

    private static void RegisterCore(Type prefabType, string key)
    {
        string normalizedKey = NormalizeRequired(key, nameof(key));
        var names = new[] { prefabType.Name, prefabType.FullName ?? prefabType.Name };
        Func<ObjectPrefab> constructor = () =>
        {
            try
            {
                return (ObjectPrefab)Activator.CreateInstance(prefabType)!;
            }
            catch (Exception exception)
            {
                throw new ObjectPrefabCreationException(normalizedKey, prefabType, exception);
            }
        };

        var descriptor = new ObjectPrefabDescriptor(normalizedKey, prefabType.Name, prefabType, names, constructor);
        ByKey[normalizedKey] = descriptor;
        foreach (string alias in descriptor.Aliases)
            ByAlias[alias] = descriptor;
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Registration key must not be empty.", parameterName);
        return value.Trim();
    }
}

/// <summary>Constructor failure surfaced by ObjectPrefabRegistry.TryCreate.</summary>
public sealed class ObjectPrefabCreationException : Exception
{
    internal ObjectPrefabCreationException(string key, Type prefabType, Exception innerException)
        : base($"Failed to construct prefab '{key}' ({prefabType.FullName}): {innerException.Message}", innerException)
    {
    }
}
