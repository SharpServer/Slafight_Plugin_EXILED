using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMER.Features;
using ProjectMER.Features.Interfaces;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Interface;

namespace Slafight_Plugin_EXILED.CustomMaps.Bridges;

/// <summary>
/// ProjectMER の <see cref="ObjectPrefabInfoRegistry"/> に、SlafightのObjectPrefab登録情報
/// （利用可能なPrefabType一覧・各PrefabTypeのOption定義）を提供するブリッジ。
/// ProjectMER側は本クラスやSlafightの存在を一切知らない（<see cref="IObjectPrefabInfoProvider"/> 経由の疎結合）。
/// </summary>
public sealed class ObjectPrefabInfoProviderBridge : IObjectPrefabInfoProvider, IBootstrapHandler
{
    private static ObjectPrefabInfoProviderBridge? _instance;

    public static void Register()
    {
        _instance ??= new ObjectPrefabInfoProviderBridge();
        ObjectPrefabInfoRegistry.Provider = _instance;
    }

    public static void Unregister()
    {
        if (ReferenceEquals(ObjectPrefabInfoRegistry.Provider, _instance))
            ObjectPrefabInfoRegistry.Provider = null;

        _instance = null;
    }

    public IReadOnlyList<ObjectPrefabTypeInfo> GetPrefabTypes()
        => ObjectPrefabRegistry.All
            .Select(descriptor => new ObjectPrefabTypeInfo
            {
                Key = descriptor.Key,
                DisplayName = descriptor.DisplayName,
                Aliases = descriptor.Aliases,
            })
            .ToArray();

    public bool TryGetOptionDefinitions(string prefabTypeName, out IReadOnlyList<ObjectPrefabOptionInfo> definitions, out string error)
    {
        definitions = Array.Empty<ObjectPrefabOptionInfo>();

        if (!ObjectPrefabRegistry.TryResolveDescriptor(prefabTypeName, out ObjectPrefabDescriptor descriptor, out error, allowFuzzy: true))
            return false;

        if (!ObjectPrefabRegistry.TryCreate(descriptor, out ObjectPrefab instance, out error))
            return false;

        definitions = instance.GetOptionDefinitions()
            .Select(definition => new ObjectPrefabOptionInfo
            {
                Name = definition.Name,
                ValueType = definition.ValueType,
                DefaultValue = definition.DefaultValue,
                ConstraintDescription = definition.ConstraintDescription,
            })
            .ToArray();
        return true;
    }
}
