using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Structs;

public struct WObjectInfo
{
    public object Object;
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Scale;

    public WObjectInfo(object @object, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        Object = @object;
        Position = position;
        Rotation = rotation;
        Scale = scale;
    }
}