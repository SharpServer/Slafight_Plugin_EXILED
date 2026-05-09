using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Mirror;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Features;

public static class GeneratorPrefab
{
    public static GameObject? Create(Vector3 position, Quaternion rotation, Vector3? scale = null)
    {
        Log.Debug("Creating Generator Prefab");
        var result = scale ?? Vector3.one;
        var obj = PrefabHelper.Spawn(PrefabType.GeneratorStructure, position, rotation);
        obj.SetWorldScale(result);
        
        NetworkServer.UnSpawn(obj);
        NetworkServer.Spawn(obj);
        var component = obj?.GetComponent<NetworkIdentity>();
        if (component is null)
        {
            Log.Error("Generator Prefab Create is failed. NetworkIdentity is null!");
        }
        else
        {
            Log.Debug($"Generator Prefab Create is successfully processed!\nNetworkIdentityInfo:\n - hasSpawned:{component.hasSpawned}\n - isServerOnly:{component.isServerOnly}\n - SpawnedFromInstantiate:{component.SpawnedFromInstantiate}\n - visible:{component.visible}");
        }
        return obj;
    }
}