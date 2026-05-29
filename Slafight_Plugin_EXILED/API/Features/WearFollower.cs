using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// Schematic をプレイヤーに追従させる MonoBehaviour。
/// SetParent の代わりにこれをアタッチすることでラグなし同期を実現。
/// </summary>
public class WearFollower : MonoBehaviour
{
    private Transform _target;
    private Vector3 _offset;
    private Quaternion _rotationOffset = Quaternion.identity;

    public void Initialize(Transform target, Vector3 offset = default, Quaternion? rotationOffset = null)
    {
        _target = target;
        _offset = offset;
        _rotationOffset = rotationOffset ?? Quaternion.identity;
    }

    private void Update()
    {
        if (_target == null)
        {
            Destroy(this);
            return;
        }

        // オフセットをターゲットのローカル座標系で計算
        transform.position = _target.position + _target.TransformDirection(_offset);
        transform.rotation = _target.rotation * _rotationOffset;
    }
}
