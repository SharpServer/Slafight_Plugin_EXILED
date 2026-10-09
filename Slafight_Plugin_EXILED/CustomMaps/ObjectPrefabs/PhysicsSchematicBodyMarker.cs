using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

/// <summary>外部の物理イベントから、Prefab が所有する Rigidbody を識別する。</summary>
public sealed class PhysicsSchematicBodyMarker : MonoBehaviour
{
    public Rigidbody Body { get; set; }
    public PhysicsSchematicObject Owner { get; set; }
}
