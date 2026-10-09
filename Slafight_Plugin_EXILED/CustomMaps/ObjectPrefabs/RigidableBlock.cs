using System.Collections.Generic;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

/// <summary>
/// 車両に叩かれると大きく斜め上へ吹き飛ばされ、既定ではその瞬間にブロック単位の剛体へ割れて飛び散る物理ブロック。
/// プレイヤーに押されたときの反応は、ごく小さく残す。
/// </summary>
public class RigidableBlock : PhysicsSchematicObject
{
    /// <summary>この水平速度(m/s)未満の接触ではプレイヤー押しでは動かさない。Polymer 以上の速度を想定する。</summary>
    private const float MinPushSpeed = 3f;
    /// <summary>プレイヤー速度と「接触点→ブロック中心」が一致している必要がある度合い(0-1)。</summary>
    private const float PushAlignment = 0.35f;
    /// <summary>同一プレイヤーによる連続接触のクールダウン(秒)。</summary>
    private const float PushCooldown = 0.4f;
    /// <summary>当たり判定を広げる余白(m)。</summary>
    private const float ContactPadding = 0.4f;
    /// <summary>破片の打ち出し方向を「車両進行方向」と「原点からの外側方向」でブレンドする比率。</summary>
    private const float ShatterSpread = 0.7f;
    /// <summary>破片に与えるランダムな角力インパルス。角速度は慣性テンソルで割られるため値は控えめにする。</summary>
    private const float TumbleTorque = 1.5f;
    /// <summary>接触点の目安になる CharacterController の高さの割合。</summary>
    private const float ContactHeightRatio = 0.5f;

    private static readonly Collider[] ContactCandidates = new Collider[32];

    private readonly Dictionary<uint, float> _pushTimes = [];
    private readonly List<Collider> _bodyColliders = [];
    private bool _detached;

    protected override string SchematicName => "RigidableBlock";
    public override bool DetachBlocks { get; set; } = false;

    /// <summary>車両衝突で受けるインパルスの倍率。1 が標準、0 で車両衝突に反応しない。</summary>
    public override float PowerForceMultiplier { get; set; } = 1f;

    /// <summary>同期間隔。0.02 秒なら毎フレームに近い追従になり、飛ぶ動きも滑らかになる。</summary>
    public override float SyncInterval { get; set; } = 0.02f;

    /// <summary>動いている間だけクライアント側の補間を使い、静止時は位置をスナップで合わせる。</summary>
    public override byte MovementSmoothing { get; set; } = 60;

    /// <summary>破片の飛翔を少しずつ減衰させる。0 なら無効。</summary>
    public override float LinearDamping { get; set; } = 0.35f;

    /// <summary>破片の回転減衰。大きいほど着地後のガクつきがquickly収まる。</summary>
    public override float AngularDamping { get; set; } = 0.8f;

    /// <summary>車両に叩かれた瞬間にブロック単位の剛体へ割れて、破片ごとに斜め上へ飛び散らせる。</summary>
    public bool DetachOnImpact { get; set; } = true;

    /// <summary>破片へ与える打ち出し速度のばらつき(倍)。</summary>
    public float ShatterSpeedVariation { get; set; } = 0.25f;

    /// <summary>プレイヤーに押されたときの押し込みの強さ(m/s)。0 で無反応。</summary>
    public float PlayerPushSpeed { get; set; } = 3.5f;

    /// <summary>プレイヤー押し時に加算する上向き速度(m/s)。</summary>
    public float PlayerPushUpwardSpeed { get; set; } = 1.2f;

    protected override void OnRoundRestarting()
    {
        _pushTimes.Clear();
        _detached = false;
        base.OnRoundRestarting();
    }

    protected override void OnPhysicsTick() => HandlePlayerPushes();

    /// <summary>車両衝突は本位の演出なので、割れた破片を扇状に斜め上へ打ち出す。</summary>
    public override void HandleExternalImpact(
        Rigidbody body,
        Vector3 direction,
        float launchSpeed,
        float upwardSpeed)
    {
        if (!_detached && DetachOnImpact && DetachPhysicsBodies())
        {
            _detached = true;
            Shatter(direction, launchSpeed, upwardSpeed);
            return;
        }

        base.HandleExternalImpact(body, direction, launchSpeed, upwardSpeed);
    }

    private void Shatter(Vector3 direction, float launchSpeed, float upwardSpeed)
    {
        // 破片は自身の重心からばらけさせつつ、車両進行方向へ大きく飛ばす。
        Vector3 origin = Vector3.zero;
        int count = 0;
        foreach (Rigidbody fragment in PhysicsBodies)
        {
            if (fragment == null)
                continue;

            origin += fragment.worldCenterOfMass;
            count++;
        }

        if (count > 0)
            origin /= count;

        foreach (Rigidbody fragment in PhysicsBodies)
        {
            if (fragment == null || fragment.isKinematic)
                continue;

            Vector3 outward = fragment.worldCenterOfMass - origin;
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.04f)
                outward = direction;
            else
                outward.Normalize();

            float variation = 1f + UnityEngine.Random.Range(-ShatterSpeedVariation, ShatterSpeedVariation);
            LaunchBody(
                fragment,
                Vector3.Slerp(direction, outward, ShatterSpread).normalized,
                launchSpeed * variation,
                upwardSpeed * variation,
                TumbleTorque);
        }
    }

    /// <summary>
    /// プレイヤーは CharacterController なので、動的 Rigidbody 側の衝突コールバックでは拾えない。
    /// 物理本体の周囲を直接オーバーラップし、触れているプレイヤーの速度から軽く揺らす。
    /// </summary>
    private void HandlePlayerPushes()
    {
        foreach (Rigidbody body in PhysicsBodies)
        {
            if (body == null || body.isKinematic || !TryGetBodyBounds(body, out Bounds bounds))
                continue;

            int count = Physics.OverlapBoxNonAlloc(
                bounds.center,
                bounds.extents + Vector3.one * ContactPadding,
                ContactCandidates,
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                if (ContactCandidates[i] is CharacterController controller)
                    TryNudgePlayer(body, controller);
            }
        }
    }

    /// <summary>Rigidbody 自身に Bounds がないため、所属 Collider からワールドAABBを組み立てる。</summary>
    private bool TryGetBodyBounds(Rigidbody body, out Bounds bounds)
    {
        bounds = default;
        _bodyColliders.Clear();
        body.GetComponentsInChildren(includeInactive: false, _bodyColliders);
        if (_bodyColliders.Count == 0)
            return false;

        bounds = _bodyColliders[0].bounds;
        for (int i = 1; i < _bodyColliders.Count; i++)
            bounds.Encapsulate(_bodyColliders[i].bounds);

        return true;
    }

    private void TryNudgePlayer(Rigidbody body, CharacterController controller)
    {
        if (PlayerPushSpeed <= 0f || PlayerPushUpwardSpeed <= 0f)
            return;

        ReferenceHub hub = controller.GetComponentInParent<ReferenceHub>();
        if (hub == null)
            return;

        Vector3 velocity = controller.velocity;
        float speed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
        if (speed < MinPushSpeed)
            return;

        // ブロックへ突っ込んでいる接触だけを反応対象にする。横を通り抜けるだけでは動かない。
        Vector3 toBlock = body.worldCenterOfMass - controller.transform.position;
        toBlock.y = 0f;
        if (toBlock.sqrMagnitude < 0.01f)
            return;

        Vector3 direction = toBlock / Mathf.Sqrt(toBlock.sqrMagnitude);
        if (Vector3.Dot(velocity, direction) < speed * PushAlignment)
            return;

        float now = Time.time;
        if (_pushTimes.TryGetValue(hub.netId, out float lastNudge) && now - lastNudge < PushCooldown)
            return;

        _pushTimes[hub.netId] = now;
        LaunchBody(body, direction, PlayerPushSpeed, PlayerPushUpwardSpeed);
    }
}
