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
    private static readonly HashSet<int> TentacleNpcIds = [];
    private static bool _eventsRegistered;

    // ===== 調整パラメータ（マップから Option としても上書き可能）=====

    /// <summary>触手の最大体力。これを削り切ると破壊される。</summary>
    public float MaxHealth { get; set; } = 150f;

    /// <summary>触手の見た目・当たり判定の高さ（m）。</summary>
    public float ColumnHeight { get; set; } = 2.6f;

    /// <summary>根本の太さ（半径 m）。</summary>
    public float BaseThickness { get; set; } = 0.32f;

    /// <summary>先端の太さ（半径 m）。</summary>
    public float TipThickness { get; set; } = 0.08f;

    /// <summary>球体の節数。多いほど滑らかで太く見える。</summary>
    public int SphereCount { get; set; } = 9;

    /// <summary>攻撃判定の半径（m）。</summary>
    public float AttackRange { get; set; } = 3.5f;

    /// <summary>1 回の攻撃ダメージ。</summary>
    public float AttackDamage { get; set; } = 35f;

    /// <summary>攻撃の間隔（秒）。</summary>
    public float AttackInterval { get; set; } = 2.5f;

    /// <summary>対象を捕捉している間の最大しなり距離（m）。</summary>
    public float TargetLeanDistance { get; set; } = 1.15f;

    /// <summary>攻撃時に先端が対象方向へ伸びる距離（m）。</summary>
    public float StrikeForwardDistance { get; set; } = 1.45f;

    /// <summary>攻撃後に一瞬引き戻る距離（m）。</summary>
    public float StrikeRecoilDistance { get; set; } = 0.45f;

    /// <summary>攻撃時の横振れ距離（m）。</summary>
    public float StrikeSideWhipDistance { get; set; } = 0.35f;

    /// <summary>攻撃モーションが続く時間（秒）。</summary>
    public float StrikeDuration { get; set; } = 0.55f;

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
    private float _strikeStartedAt = -100f;
    private Vector3 _strikeDirection = Vector3.forward;

    public static void RegisterEvents()
    {
        if (_eventsRegistered)
            return;

        Exiled.Events.Handlers.Player.SpawningRagdoll += OnSpawningRagdoll;
        _eventsRegistered = true;
    }

    public static void UnregisterEvents()
    {
        if (!_eventsRegistered)
            return;

        Exiled.Events.Handlers.Player.SpawningRagdoll -= OnSpawningRagdoll;
        TentacleNpcIds.Clear();
        _eventsRegistered = false;
    }

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

        TentacleNpcIds.Add(_npc.Id);
        _npc.HideNpcFromClientPlayerList("Tentacle:spawn");

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
        float horizontalScale = Mathf.Clamp(BaseThickness * 1.6f, 0.35f, 0.8f);
        _npc.Scale = new Vector3(horizontalScale, verticalScale, horizontalScale);

        _npc.HideNpcFromClientPlayerList("Tentacle:post-spawn");
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
        SetHealthCustomInfo(_npc, $"Tentacle HP {cur} of {max}");
    }

    private static void SetHealthCustomInfo(Player npc, string info)
    {
        npc.CustomInfo = info;
        npc.InfoArea |= PlayerInfoArea.CustomInfo;
        npc.InfoArea &= ~PlayerInfoArea.Nickname;
        npc.InfoArea &= ~PlayerInfoArea.Role;
        npc.InfoArea &= ~PlayerInfoArea.UnitName;
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
            {
                _strikeDirection = _leanDirection.sqrMagnitude > 0.0001f ? _leanDirection : toTarget.normalized;
                _strikeStartedAt = Time.time;
                target.Hurt(AttackDamage, "SCP-035の触手に襲われた");
            }
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
        Vector3 lean = _leanDirection * (_leanAmount * TargetLeanDistance);
        float strikeDuration = Mathf.Max(0.1f, StrikeDuration);
        float strikePhase = Mathf.Clamp01((time - _strikeStartedAt) / strikeDuration);
        bool isStriking = strikePhase < 1f;
        float strikeForwardPulse = isStriking ? Mathf.Sin(strikePhase * Mathf.PI) : 0f;
        float strikeRecoilPhase = isStriking ? Mathf.Clamp01((strikePhase - 0.55f) / 0.45f) : 1f;
        float strikeRecoilPulse = isStriking ? Mathf.Sin(strikeRecoilPhase * Mathf.PI) : 0f;
        float strikeSidePulse = isStriking ? Mathf.Sin(strikePhase * Mathf.PI * 2f) * (1f - strikePhase) : 0f;
        Vector3 strikeSideDirection = new(-_strikeDirection.z, 0f, _strikeDirection.x);

        for (int i = 0; i < count; i++)
        {
            Primitive sphere = _spheres[i];
            if (sphere?.Base == null)
                continue;

            float t = i / (float)(count - 1); // 0=根本, 1=先端
            float height = t * ColumnHeight;

            // 軽い「うねり」と攻撃時のしなり（先端ほど大きく）。
            float sway = Mathf.Sin(time * 3.1f + t * 5.5f) * 0.16f * t;
            float tipWeight = t * t;
            Vector3 strikeForward =
                _strikeDirection *
                ((strikeForwardPulse * StrikeForwardDistance - strikeRecoilPulse * StrikeRecoilDistance) * tipWeight);
            Vector3 strikeSide = strikeSideDirection * (strikeSidePulse * StrikeSideWhipDistance * t);
            float strikeWave = isStriking
                ? Mathf.Sin((t * 3.2f - strikePhase * 4.4f) * Mathf.PI) * (1f - strikePhase) * 0.16f * t
                : 0f;
            Vector3 sideOffset = lean * tipWeight + strikeForward + strikeSide + new Vector3(sway, 0f, sway * 0.5f);

            sphere.Position = basePos + Vector3.up * (height + strikeWave) + sideOffset;
            float radius = Mathf.Lerp(BaseThickness, TipThickness, t);
            float strikeScale = 1f + strikeForwardPulse * Mathf.Lerp(0.08f, 0.38f, t);
            sphere.Scale = Vector3.one * (radius * 2f * strikeScale);
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
            int npcId = _npc.Id;
            try
            {
                _npc.Destroy();
            }
            catch
            {
                // ignore
            }

            _npc = null;
            TentacleNpcIds.Remove(npcId);
        }

        base.OnDestroy();
    }

    private static void OnSpawningRagdoll(Exiled.Events.EventArgs.Player.SpawningRagdollEventArgs ev)
    {
        if (ev?.Player != null && TentacleNpcIds.Contains(ev.Player.Id))
            ev.IsAllowed = false;
    }
}
