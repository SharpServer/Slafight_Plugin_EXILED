using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Toys;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

/// <summary>
/// SCP-035 の触手。HIDTurret と同じく Fade で不可視化した縦長の NPC を当たり判定の核とし、
/// 見た目は球体プリミティブを縦に積んだ太い触手で表現する。
/// NPC の体力を削り切ると破壊できる（無敵ではない）。NPC の CustomInfo に体力を表示する。
/// </summary>
public class Tentacle : ObjectPrefab
{
    // ===== 調整パラメータ（マップから Option としても上書き可能）=====

    /// <summary>触手の最大体力。これを削り切ると破壊される。</summary>
    public float MaxHealth { get; set; } = 150f;

    /// <summary>触手の見た目・当たり判定の高さ（m）。</summary>
    public float ColumnHeight { get; set; } = 4f;

    /// <summary>根本の太さ（半径 m）。</summary>
    public float BaseThickness { get; set; } = 0.55f;

    /// <summary>先端の太さ（半径 m）。</summary>
    public float TipThickness { get; set; } = 0.13f;

    /// <summary>球体の節数。多いほど滑らかで太く見える。</summary>
    public int SphereCount { get; set; } = 12;

    /// <summary>攻撃判定の半径（m）。</summary>
    public float AttackRange { get; set; } = 3.5f;

    /// <summary>1 回の攻撃ダメージ。</summary>
    public float AttackDamage { get; set; } = 35f;

    /// <summary>攻撃の間隔（秒）。</summary>
    public float AttackInterval { get; set; } = 2.5f;

    public override bool FollowMarkerTransform => false;

    private static readonly Color TentacleColor = new(0.22f, 0.02f, 0.05f, 1f);
    private const float UpdateInterval = 1f / 30f;
    private const float FpcCapsuleHeight = 1.8f; // FPC ロールの素のカプセル高さ目安

    private Npc? _npc;
    private readonly List<Primitive> _spheres = [];
    private CoroutineHandle _updateHandle;
    private bool _isDestroying;

    private float _lastShownHealth = -1f;
    private float _nextAttackTime;
    private float _leanAmount;       // 攻撃時に対象へしなる量（0..1）
    private Vector3 _leanDirection;  // しなる方向（水平）

    protected override void OnCreate()
    {
        // NPC の Position は「足元」基準（HIDTurret 参照）。触手の根本＝足元に合わせる。
        _npc = Npc.Spawn("Tentacle", RoleTypeId.Scp0492, true, Position);
        if (_npc == null)
        {
            Log.Error("[Tentacle] Failed to spawn NPC.");
            Destroy();
            return;
        }

        BuildSpheres();

        // ロール適用が遅延するため、完了を待ってから NPC を設定する。
        ScheduleDelayed(Npc.SpawnSetRoleDelay + 0.1f, ConfigureNpc);
        base.OnCreate();
    }

    private void ConfigureNpc()
    {
        if (_npc is not { IsAlive: true })
        {
            Destroy();
            return;
        }

        // 縦長の当たり判定。Fade で見た目だけ消す（カプセルは残るので球体を撃てば当たる）。
        float verticalScale = Mathf.Max(1f, ColumnHeight / FpcCapsuleHeight);
        _npc.Scale = new Vector3(0.8f, verticalScale, 0.8f);

        _npc.IsNoclipPermitted = true;
        _npc.IsNoclipEnabled = true;   // 重力で落ちず、配置位置に固定
        _npc.IsGodModeEnabled = false; // ダメージを受けて破壊できるようにする
        _npc.IsSpectatable = false;
        _npc.EnableEffect(EffectType.Fade, 255);

        _npc.MaxHealth = MaxHealth;
        _npc.Health = MaxHealth;
        _npc.Position = Position;

        UpdateHealthInfo(force: true);
        _nextAttackTime = Time.time + AttackInterval;
        _updateHandle = Timing.RunCoroutine(UpdateCoroutine());
    }

    private void BuildSpheres()
    {
        DestroySpheres();
        int count = Mathf.Max(2, SphereCount);
        for (int i = 0; i < count; i++)
        {
            Primitive sphere = Primitive.Create(
                PrimitiveType.Sphere, Vector3.zero, Vector3.zero, Vector3.one * 0.1f, true, TentacleColor);
            sphere.Collidable = false; // 弾やプレイヤーの移動を邪魔しない
            _spheres.Add(sphere);
        }
    }

    private IEnumerator<float> UpdateCoroutine()
    {
        while (!_isDestroying)
        {
            if (_npc is not { IsAlive: true })
            {
                // 体力が尽きた（撃破された）→ 触手も破壊。
                Destroy();
                yield break;
            }

            // 配置位置（足元＝根本）に固定し続ける。
            _npc.Position = Position;

            UpdateHealthInfo();
            UpdateAttack();
            AnimateSpheres();

            yield return Timing.WaitForSeconds(UpdateInterval);
        }
    }

    private void UpdateHealthInfo(bool force = false)
    {
        if (_npc == null)
            return;

        float health = Mathf.Max(0f, _npc.Health);
        if (!force && Mathf.Approximately(health, _lastShownHealth))
            return;

        _lastShownHealth = health;
        int cur = Mathf.CeilToInt(health);
        int max = Mathf.CeilToInt(MaxHealth);
        string color = health > MaxHealth * 0.5f ? "#7CFC00" : health > MaxHealth * 0.25f ? "#FFD700" : "#FF3030";
        _npc.SetCustomInfo($"<b>Tentacle</b>\n<color={color}>HP {cur}/{max}</color>");
    }

    private void UpdateAttack()
    {
        if (_npc == null)
            return;

        Player? target = FindTarget();
        if (target == null)
        {
            _leanAmount = Mathf.MoveTowards(_leanAmount, 0f, UpdateInterval * 2f);
            return;
        }

        // 対象方向へしならせる。
        Vector3 toTarget = target.Position - Position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude > 0.0001f)
            _leanDirection = toTarget.normalized;
        _leanAmount = Mathf.MoveTowards(_leanAmount, 1f, UpdateInterval * 3f);

        if (Time.time >= _nextAttackTime)
        {
            _nextAttackTime = Time.time + AttackInterval;
            if (target.IsAlive && Vector3.Distance(target.Position, Position) <= AttackRange)
                target.Hurt(AttackDamage, "SCP-035の触手に襲われた");
        }
    }

    private Player? FindTarget()
    {
        Player? nearest = null;
        float nearestSqr = AttackRange * AttackRange;
        foreach (Player player in Player.List)
        {
            if (player == null || player is Npc || !player.IsAlive || player.GetTeam() == CTeam.SCPs)
                continue;

            Vector3 delta = player.Position - Position;
            delta.y = 0f;
            float sqr = delta.sqrMagnitude;
            if (sqr > nearestSqr)
                continue;

            nearestSqr = sqr;
            nearest = player;
        }

        return nearest;
    }

    private void AnimateSpheres()
    {
        int count = _spheres.Count;
        if (count == 0)
            return;

        Vector3 basePos = Position;
        float time = Time.time;
        // 攻撃時はしなりを横方向の変位として上部ほど強く効かせる。
        Vector3 lean = _leanDirection * (_leanAmount * 0.9f);

        for (int i = 0; i < count; i++)
        {
            Primitive sphere = _spheres[i];
            if (sphere?.Base == null)
                continue;

            float t = i / (float)(count - 1); // 0=根本, 1=先端
            float height = t * ColumnHeight;

            // 軽い「うねり」と攻撃時のしなり（先端ほど大きく）。
            float sway = Mathf.Sin(time * 2.2f + t * 4f) * 0.12f * t;
            Vector3 sideOffset = lean * (t * t) + new Vector3(sway, 0f, sway * 0.5f);

            sphere.Position = basePos + Vector3.up * height + sideOffset;
            float radius = Mathf.Lerp(BaseThickness, TipThickness, t);
            sphere.Scale = Vector3.one * (radius * 2f);
        }
    }

    private void DestroySpheres()
    {
        foreach (Primitive sphere in _spheres)
        {
            try
            {
                sphere?.RemoveShowState();
                sphere?.Destroy();
            }
            catch
            {
                // ignore
            }
        }

        _spheres.Clear();
    }

    protected override void OnDestroy()
    {
        if (_isDestroying)
            return;

        _isDestroying = true;

        if (_updateHandle.IsRunning)
            Timing.KillCoroutines(_updateHandle);

        DestroySpheres();

        if (_npc != null)
        {
            try
            {
                _npc.Destroy();
            }
            catch
            {
                // ignore
            }

            _npc = null;
        }

        base.OnDestroy();
    }
}
