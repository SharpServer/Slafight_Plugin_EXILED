using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Interface;

public interface IObjectPrefab
{
    string ObjectInstanceID { get; set; }
    string Tag { get; set; }
    Vector3 Position { get; set; }
    Quaternion Rotation { get; set; }
    Vector3 Scale { get; set; }
}
