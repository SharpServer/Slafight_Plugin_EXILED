using System.Collections;
using System.Collections.Generic;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps;

/// <summary>リスポーン車両の接近を物理スキマティックへ伝えるサーバー側の衝突ソース。</summary>
internal sealed class RespawnVehiclePhysicsImpact : MonoBehaviour
{
    /// <summary>車両進行方向へ跳出させる水平速度(m/s)。</summary>
    private const float ImpactSpeed = 36f;
    /// <summary>同時に加算する上向き速度(m/s)。値が大きいほど斜め上へ高く飛ぶ。</summary>
    private const float UpwardSpeed = 26f;
    /// <summary>擬似車両の走行速度(m/s)。クライアント側の演出速度に合わせたければここを触る。</summary>
    private const float GhostSpeed = 24f;
    /// <summary>擬似車両を車庫位置の手前側へ走らせる距離(m)。</summary>
    private const float GhostBackDistance = 20f;
    /// <summary>擬似車両を車庫位置の進行方向側へ走らせる距離(m)。</summary>
    private const float GhostForwardDistance = 80f;
    /// <summary>mesh bounds を取得できない場合の車体半幅(m)。実車相当に寄せた値。</summary>
    private static readonly Vector3 FallbackHalfExtents = new(1.1f, 1.3f, 2.9f);

    private readonly HashSet<int> _impactedBodies = new();
    private static readonly List<RespawnVehiclePhysicsImpact> Instances = new();
    private Renderer[] _renderers;
    private string _vehicleName;
    private Bounds _previousBounds;
    private Vector3 _travelDirection;
    private Coroutine _ghostRoutine;
    private bool _hasPreviousBounds;
    private bool _reportedMovement;

    /// <summary>現在用意されている車両 impact 一覧です。デバッグ描画がこれを回します。</summary>
    internal static IReadOnlyList<RespawnVehiclePhysicsImpact> ActiveInstances => Instances;

    private void OnDestroy() => Instances.Remove(this);

    public static void Prepare(GameObject vehicle, string vehicleName)
    {
        if (vehicle == null)
        {
            Log.Warn($"[RespawnVehiclePhysicsImpact] {vehicleName} was not found.");
            return;
        }

        RespawnVehiclePhysicsImpact impact = vehicle.GetComponentInChildren<RespawnVehiclePhysicsImpact>(true);
        if (impact == null)
        {
            GameObject proxy = new($"{vehicleName}PhysicsImpact");
            proxy.transform.SetParent(vehicle.transform, false);
            impact = proxy.AddComponent<RespawnVehiclePhysicsImpact>();
        }

        if (!Instances.Contains(impact))
            Instances.Add(impact);

        impact._vehicleName = vehicleName;
        impact._renderers = vehicle.GetComponentsInChildren<Renderer>(true);
        impact._impactedBodies.Clear();
        impact._hasPreviousBounds = false;
        impact._reportedMovement = false;
        if (impact._renderers.Length == 0)
            Log.Warn($"[RespawnVehiclePhysicsImpact] {vehicleName} has no renderers for collision bounds yet.");
        else
            Log.Info(
                $"[RespawnVehiclePhysicsImpact] {vehicleName} prepared with {impact._renderers.Length} renderers " +
                $"at {Format(vehicle.transform.position)} facing {Format(vehicle.transform.forward)}.");

        StartServerAnimation(vehicle, vehicleName);
        impact.enabled = true;
        impact.RestartGhostSweep();
    }

    /// <summary>
    /// 車両の到着演出はクライアントのウェーブアニメーションなので、サーバー側の車両 Transform は
    /// 動かない。実際の Bounds 変化を待つと衝突判定が一度も発火しないため、
    /// 同じ大きさの箱を車庫位置の手前から走らせる擬似車両で確実に判定させる。
    /// </summary>
    private void RestartGhostSweep()
    {
        if (_ghostRoutine != null)
        {
            StopCoroutine(_ghostRoutine);
            _ghostRoutine = null;
        }

        if (!TryGetVehicleShape(out Vector3 center, out Vector3 halfExtents, out Quaternion rotation))
            return;

        _ghostRoutine = StartCoroutine(RunGhostSweep(center, halfExtents, rotation));
    }

    /// <summary>
    /// 箱の実寸(m)を手動で固定する場合の値。null なら mesh bounds から求める。
    /// </summary>
    /// <remarks>
    /// `slc hitbox van size &lt;x&gt; &lt;y&gt; &lt;z&gt;` で設定します。
    /// 実寸(全幅)ではなく半径ではないので、値そのものは車体の実寸です。
    /// </remarks>
    public static Vector3? SizeOverride { get; set; }

    /// <summary>
    /// 箱の中心を車両ローカル系へずらす量(m)。
    /// </summary>
    /// <remarks>
    /// `slc hitbox van offset &lt;x&gt; &lt;y&gt; &lt;z&gt;` で設定します。ワールド座標ではなく
    /// 車両ローカル軸なので、車両がどこを向いていても「前に 1m」「上に 0.3m」で指定できます。
    /// </remarks>
    public static Vector3 CenterOffset { get; set; } = Vector3.zero;

    /// <summary>
    /// 走行中のスイープに固态化される前に、現在の判定箱を取得します。
    /// </summary>
    /// <remarks>
    /// デバッグ描画と実際の判定が同じ値を使うための口です。
    /// </remarks>
    public bool TryGetCurrentShape(out Vector3 center, out Vector3 halfExtents, out Quaternion rotation)
        => TryGetVehicleShape(out center, out halfExtents, out rotation);

    /// <summary>
    /// 車両.Root のローカル空間で Collider 相当の箱を作る。
    /// SkinnedMeshRenderer の bounds はアニメ全域を含むため実寸より遥かに大きくなるので、
    /// クライアントアセットの mesh bounds（rest pose）を車両ルートへ変換して使う。
    /// </summary>
    private bool TryGetVehicleShape(out Vector3 center, out Vector3 halfExtents, out Quaternion rotation)
    {
        Transform vehicle = transform.parent;
        rotation = vehicle.rotation;
        center = vehicle.position;
        halfExtents = FallbackHalfExtents;
        if (SizeOverride is { } manualSize)
        {
            halfExtents = new Vector3(
                Mathf.Max(manualSize.x * 0.5f, 0.05f),
                Mathf.Max(manualSize.y * 0.5f, 0.05f),
                Mathf.Max(manualSize.z * 0.5f, 0.05f));
            center = vehicle.position + rotation * CenterOffset;
            return true;
        }

        if (_renderers == null || _renderers.Length == 0)
            return false;

        Matrix4x4 worldToVehicle = vehicle.worldToLocalMatrix;
        bool found = false;
        Vector3 localMin = Vector3.zero;
        Vector3 localMax = Vector3.zero;
        foreach (Renderer renderer in _renderers)
        {
            if (!TryGetMeshBounds(renderer, out Bounds meshBounds))
                continue;

            Matrix4x4 toVehicle = worldToVehicle * renderer.transform.localToWorldMatrix;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 offset = new(
                    (corner & 1) == 0 ? -meshBounds.extents.x : meshBounds.extents.x,
                    (corner & 2) == 0 ? -meshBounds.extents.y : meshBounds.extents.y,
                    (corner & 4) == 0 ? -meshBounds.extents.z : meshBounds.extents.z);
                Vector3 local = toVehicle.MultiplyPoint3x4(meshBounds.center + offset);
                if (!found)
                {
                    localMin = local;
                    localMax = local;
                    found = true;
                }
                else
                {
                    localMin = Vector3.Min(localMin, local);
                    localMax = Vector3.Max(localMax, local);
                }
            }
        }

        if (!found)
            return false;

        Vector3 size = localMax - localMin;
        halfExtents = new Vector3(
            Mathf.Max(size.x * 0.5f, 0.4f),
            Mathf.Max(size.y * 0.5f, 0.4f),
            Mathf.Max(size.z * 0.5f, 0.4f));
        Vector3 localCenter = (localMin + localMax) * 0.5f + CenterOffset;
        center = vehicle.TransformPoint(localCenter);
        return true;
    }

    /// <summary>Renderer が持つ mesh のローカル bounds を返す。Particle / Trail は寸法が当てにならないため除外。</summary>
    private static bool TryGetMeshBounds(Renderer renderer, out Bounds meshBounds)
    {
        meshBounds = default;
        switch (renderer)
        {
            case SkinnedMeshRenderer skinned when skinned.sharedMesh != null:
                meshBounds = skinned.sharedMesh.bounds;
                return true;
            case MeshRenderer mesh:
                MeshFilter filter = mesh.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    return false;

                meshBounds = filter.sharedMesh.bounds;
                return true;
            default:
                return false;
        }
    }

    private IEnumerator RunGhostSweep(Vector3 arrivalCenter, Vector3 halfExtents, Quaternion rotation)
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.ProjectOnPlane(transform.up, Vector3.up) + Vector3.forward;

        forward.Normalize();

        // 走行はクライアントの演出でサーバー Transform は動かないため、
        // 車庫位置の前後を直線で覆って判定させる。
        float total = GhostBackDistance + GhostForwardDistance;
        Vector3 start = arrivalCenter - forward * GhostBackDistance;
        _travelDirection = forward;
        _previousBounds = new Bounds(start, halfExtents * 2f);
        _hasPreviousBounds = true;
        _reportedMovement = true;
        Log.Info(
            $"[RespawnVehiclePhysicsImpact] {_vehicleName} ghost sweep {Format(start)} -> " +
            $"{Format(start + forward * total)} at {GhostSpeed:F0} m/s, half extents {Format(halfExtents)}.");

        Vector3 previous = start;
        float traveled = 0f;
        while (traveled < total)
        {
            float step = GhostSpeed * Time.fixedDeltaTime;
            Vector3 current = previous + forward * step;

            // 1 物理ステップの移動は 0.5 m 未満で車体長より短いので、前後 2 箇所を見れば隙間が出ない。
            foreach (Collider collider in Physics.OverlapBox(
                         current, halfExtents, rotation, ~0, QueryTriggerInteraction.Collide))
                TryImpact(collider, current);

            foreach (Collider collider in Physics.OverlapBox(
                         previous, halfExtents, rotation, ~0, QueryTriggerInteraction.Collide))
                TryImpact(collider, current);

            previous = current;
            traveled += step;
            _previousBounds = new Bounds(current, halfExtents * 2f);
            yield return new WaitForFixedUpdate();
        }

        Log.Info($"[RespawnVehiclePhysicsImpact] {_vehicleName} ghost sweep finished.");
        _ghostRoutine = null;
    }

    private static void StartServerAnimation(GameObject vehicle, string vehicleName)
    {
        // WaveAnimationBase はクライアント専用のシーンにある。サーバー側には
        // 同じ Animator だけが残るため、車両本体の Play トリガーを直接起動する。
        Animator animator = vehicleName == "ChaosVan"
            ? vehicle.GetComponent<Animator>()
            : vehicle.transform.Find("OH-58D")?.GetComponent<Animator>();
        if (animator == null)
        {
            Log.Warn($"[RespawnVehiclePhysicsImpact] {vehicleName} server Animator was not found.");
            return;
        }

        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.ResetTrigger("Play");
        animator.SetTrigger("Play");
        Log.Info($"[RespawnVehiclePhysicsImpact] {vehicleName} server animation started on {animator.name}.");
    }

    private void FixedUpdate()
    {
        // 擬似車両が走っている間は Bounds 側の判定と二重にならないようにする。
        if (_ghostRoutine != null)
            return;

        if (_renderers == null || _renderers.Length == 0)
            _renderers = transform.parent.GetComponentsInChildren<Renderer>(true);

        UpdateBounds();
    }

    private void UpdateBounds()
    {
        bool found = false;
        Bounds bounds = default;
        foreach (Renderer renderer in _renderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!found)
        {
            _hasPreviousBounds = false;
            return;
        }

        if (_hasPreviousBounds)
        {
            Vector3 travel = bounds.center - _previousBounds.center;
            if (travel.sqrMagnitude > 0.0001f)
            {
                if (!_reportedMovement)
                {
                    Log.Info($"[RespawnVehiclePhysicsImpact] {_vehicleName} started moving; checking swept bounds for physics bodies.");
                    _reportedMovement = true;
                }

                _travelDirection = travel.normalized;
                Bounds sweptBounds = bounds;
                sweptBounds.Encapsulate(_previousBounds);
                foreach (Collider collider in Physics.OverlapBox(
                             sweptBounds.center, sweptBounds.extents, Quaternion.identity,
                             ~0, QueryTriggerInteraction.Collide))
                    TryImpact(collider, bounds.center);
            }
        }

        _previousBounds = bounds;
        _hasPreviousBounds = true;
    }

    private void TryImpact(Collider other, Vector3 vehicleCenter)
    {
        PhysicsSchematicBodyMarker marker = other.GetComponentInParent<PhysicsSchematicBodyMarker>();
        if (marker == null || marker.Body == null || marker.Body.isKinematic)
            return;

        Rigidbody body = marker.Body;
        int id = body.GetInstanceID();
        if (!_impactedBodies.Add(id))
            return;

        // 破断後は破片 1 個ずつが独立剛体になる。1 体ずつ打ち上げると合计エネルギーが
        // 破片数分だけ積み上がって落下しきれなくなるので、同一オブジェクトは 1 回だけ叩く。
        if (marker.Owner == null || marker.Owner.HasExternalImpact)
            return;

        float multiplier = marker.Owner.PowerForceMultiplier;
        if (multiplier <= 0f)
            return;

        // 打ち出し方向は常に水平へ落とす。車両が浮上・落下していても軌道が乱れない。
        Vector3 direction = _travelDirection;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f)
        {
            direction = body.worldCenterOfMass - vehicleCenter;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude < 0.01f)
            direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up);

        if (direction.sqrMagnitude < 0.01f)
            direction = Vector3.forward;

        direction.Normalize();
        float launchSpeed = ImpactSpeed * multiplier;
        float upwardSpeed = UpwardSpeed * multiplier;
        marker.Owner.HandleExternalImpact(body, direction, launchSpeed, upwardSpeed);
        Log.Info(
            $"[RespawnVehiclePhysicsImpact] {_vehicleName} hit {body.name}: " +
            $"launch={launchSpeed:F1}, upward={upwardSpeed:F1}, multiplier={multiplier:F1}.");
    }

    private static string Format(Vector3 value)
        => $"({value.x:F1}, {value.y:F1}, {value.z:F1})";
}
