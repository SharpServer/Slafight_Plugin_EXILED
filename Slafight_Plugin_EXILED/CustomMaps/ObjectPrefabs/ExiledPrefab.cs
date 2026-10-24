using System;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using MapGeneration.Distributors;
using MapGeneration.RoomConnectors;
using Mirror;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Interface;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

/// <summary>A native, networked EXILED prefab owned by an exported schematic marker.</summary>
public sealed class ExiledPrefab : ObjectPrefab, IObjectPrefabMarkerReceiver
{
    private GameObject? instance;
    private SchematicObjectPrefabObject? marker;
    private ExiledPrefabMarkerFollower? follower;
    public string NativeType { get; set; } = nameof(PrefabType.WorkstationStructure);
    // Exact registered name also supports prefabs added after the installed EXILED enum.
    public string NativeName { get; set; } = string.Empty;
    public override bool FollowMarkerTransform => false;
    protected override float SetupDelay => 0f;

    public void BindMarker(SchematicObjectPrefabObject source) => marker = source;

    protected override void OnSetup()
    {
        PrefabType type = default;
        bool known = Enum.TryParse(NativeType, true, out type) && Enum.IsDefined(typeof(PrefabType), type);
        GameObject source = known ? PrefabHelper.GetPrefab(type) : null;
        string expectedName = NativeName;
        if (source == null && string.IsNullOrWhiteSpace(expectedName) && known)
            expectedName = type.GetPrefabAttribute()?.Name;
        if (source == null && !string.IsNullOrWhiteSpace(expectedName))
            source = NetworkClient.prefabs.Values.FirstOrDefault(prefab => prefab != null &&
                string.Equals(prefab.name, expectedName, StringComparison.OrdinalIgnoreCase));
        if (source == null)
            throw new InvalidOperationException($"Native prefab '{NativeType}' ('{expectedName}') is not registered in this server build.");

        // Same initialization as PrefabHelper.Spawn, with authored scale applied BEFORE Spawn.
        // Parenting a native object to a networked schematic sends incorrect local spawn TRS.
        instance = Object.Instantiate(source, Position, Rotation);
        instance.transform.localScale = Scale;
        if (instance.TryGetComponent(out StructurePositionSync structure))
            StructureTransformSync.Attach(structure);
        if (known && (type == PrefabType.HCZBreakableDoor || type == PrefabType.HCZOneSided || type == PrefabType.HCZTwoSided) &&
            instance.TryGetComponent(out WallableSmallNodeRoomConnector connector))
            connector.Network_syncBitmask = type == PrefabType.HCZBreakableDoor ? (byte)3 : type == PrefabType.HCZOneSided ? (byte)1 : (byte)0;
        try
        {
            NetworkServer.Spawn(instance);
            follower = instance.AddComponent<ExiledPrefabMarkerFollower>();
            follower.Owner = this;
            follower.Source = marker;
            follower.Remember();
        }
        catch
        {
            DestroyNative();
            throw;
        }
    }

    public override void SyncManagedObjects()
    {
        base.SyncManagedObjects();
        if (instance == null) return;
        instance.transform.SetPositionAndRotation(Position, Rotation);
        instance.transform.localScale = Scale;
        if (instance.TryGetComponent(out StructurePositionSync structure))
            StructureTransformSync.Attach(structure);
        if (instance.TryGetComponent(out NetworkIdentity identity) && identity.netId != 0 && NetworkServer.active)
        {
            NetworkServer.UnSpawn(instance);
            NetworkServer.Spawn(instance);
        }
    }

    internal void UpdateMarkerPose()
    {
        if (marker == null) return;
        // The property setters synchronize; defer them until the complete pose is assigned.
        GameObject current = instance;
        instance = null;
        Position = marker.transform.position;
        Rotation = marker.transform.rotation;
        Scale = marker.transform.lossyScale;
        instance = current;
        SyncManagedObjects();
    }

    private void DestroyNative()
    {
        if (follower != null) { follower.Owner = null; follower.Source = null; follower.enabled = false; }
        follower = null;
        if (instance != null)
        {
            if (NetworkServer.active && instance.TryGetComponent(out NetworkIdentity identity) && identity.netId != 0)
                NetworkServer.Destroy(instance);
            else Object.Destroy(instance);
        }
        instance = null;
        marker = null;
    }

    protected override void OnDestroy() => DestroyNative();
    protected override void OnRoundRestarting() => DestroyNative();
}

/// <summary>Only authored marker changes move a prefab; native physics remains free to run.</summary>
internal sealed class ExiledPrefabMarkerFollower : MonoBehaviour
{
    internal ExiledPrefab? Owner;
    internal SchematicObjectPrefabObject? Source;
    private Vector3 position, scale;
    private Quaternion rotation;
    internal void Remember()
    {
        if (Source == null) return;
        position = Source.transform.position;
        rotation = Source.transform.rotation;
        scale = Source.transform.lossyScale;
    }
    private void LateUpdate()
    {
        if (Owner == null || Source == null) return;
        if (position == Source.transform.position && rotation == Source.transform.rotation && scale == Source.transform.lossyScale) return;
        Owner.UpdateMarkerPose();
        Remember();
    }
}
