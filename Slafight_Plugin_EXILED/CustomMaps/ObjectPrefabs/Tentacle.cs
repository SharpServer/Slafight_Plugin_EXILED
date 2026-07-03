using System;
using System.Collections.Generic;
using AdminToys;
using Exiled.API.Enums;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

/// <summary>
/// SCP-035 の触手。見た目は以前の Tentacle schematic を使い、衝突は不可視 NPC 側で受ける。
/// NPC の体力を削り切ると破壊できる（無敵ではない）。NPC の CustomInfo に体力を表示する。
/// </summary>
public class Tentacle : ObjectPrefab
{
    private static readonly HashSet<int> TentacleNpcIds = [];
    private static bool _eventsRegistered;

    private const string TentacleSchematicName = "Tentacle";
    private const float SpawnAnimationDuration = 1f;
    private const float DestroyAnimationDuration = 1.05f;
    private const float IdleAnimationRefreshInterval = 5f;
    private const float UpdateInterval = 1f / 30f;
    private const float FpcCapsuleHeight = 1.8f; // FPC ロールの素のカプセル高さ目安

    // ===== 調整パラメータ（マップから Option としても上書き可能）=====

    /// <summary>触手の最大体力。これを削り切ると破壊される。</summary>
    public float MaxHealth { get; set; } = 1000f;

    /// <summary>NPC 側の当たり判定の高さ（m）。</summary>
    public float ColumnHeight { get; set; } = 2f;

    /// <summary>NPC 側の当たり判定の太さ（半径 m）。</summary>
    public float BaseThickness { get; set; } = 0.12f;

    /// <summary>攻撃判定の半径（m）。</summary>
    public float AttackRange { get; set; } = 1.85f;

    /// <summary>1 回の攻撃ダメージ。</summary>
    public float AttackDamage { get; set; } = 35f;

    /// <summary>攻撃の間隔（秒）。</summary>
    public float AttackInterval { get; set; } = 2.5f;

    /// <summary>攻撃アニメーションから idle へ戻すまでの時間（秒）。</summary>
    public float StrikeDuration { get; set; } = 0.83f;

    public override bool FollowMarkerTransform => false;

    public override Vector3 Position
    {
        get => _schematicObject != null ? _schematicObject.Position : base.Position;
        set
        {
            base.Position = value;
            if (_schematicObject != null)
                _schematicObject.Position = value;
            if (_npc != null)
                _npc.Position = value;
        }
    }

    public override Quaternion Rotation
    {
        get => _schematicObject != null ? _schematicObject.Rotation : base.Rotation;
        set
        {
            base.Rotation = value;
            if (_schematicObject != null)
                _schematicObject.Rotation = value;
        }
    }

    public override Vector3 Scale
    {
        get => _schematicObject != null ? _schematicObject.Scale : base.Scale;
        set
        {
            base.Scale = value;
            if (_schematicObject != null)
                _schematicObject.Scale = value;
        }
    }

    private SchematicObject? _schematicObject;
    private ProjectMER.Features.AnimationController? _animationController;
    private Npc? _npc;
    private CoroutineHandle _updateHandle;
    private bool _isDestroying;

    private float _lastShownHealth = -1f;
    private float _nextAttackTime;
    private float _nextIdleAnimationTime;

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
        _schematicObject = ObjectSpawner.SpawnSchematic(TentacleSchematicName, base.Position, base.Rotation);
        if (_schematicObject == null)
        {
            Log.Error($"[Tentacle] Failed to spawn schematic '{TentacleSchematicName}'.");
            Destroy();
            return;
        }

        _schematicObject.Scale = base.Scale;
        DisableSchematicCollision(_schematicObject);
        PlaySchematicAnimation("spawning");
        _nextIdleAnimationTime = Time.time + SpawnAnimationDuration;

        // NPC の Position は「足元」基準（HIDTurret 参照）。触手の根本＝足元に合わせる。
        _npc = Npc.Spawn("Tentacle", RoleTypeId.Tutorial, true, Position);
        if (_npc == null)
        {
            Log.Error("[Tentacle] Failed to spawn NPC.");
            Destroy();
            return;
        }

        TentacleNpcIds.Add(_npc.Id);
        _npc.HideNpcFromClientPlayerList("Tentacle:spawn");

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

        // Schematic 側の衝突は切り、縦長の不可視 NPC だけを撃破可能な当たり判定にする。
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
            UpdateIdleAnimation();

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
            return;

        Vector3 toTarget = target.Position - Position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude > 0.0001f)
            Rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);

        if (Time.time < _nextAttackTime)
            return;

        _nextAttackTime = Time.time + AttackInterval;
        if (!target.IsAlive || Vector3.Distance(target.Position, Position) > AttackRange)
            return;

        PlaySchematicAnimation("attacking");
        _nextIdleAnimationTime = Time.time + Mathf.Max(0.1f, StrikeDuration);
        target.Hurt(AttackDamage, "SCP-035の触手に襲われた");
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

    private void UpdateIdleAnimation()
    {
        if (_schematicObject == null || Time.time < _nextIdleAnimationTime)
            return;

        PlaySchematicAnimation("idle");
        _nextIdleAnimationTime = Time.time + IdleAnimationRefreshInterval;
    }

    private void PlaySchematicAnimation(string stateName)
    {
        if (_schematicObject == null)
            return;

        try
        {
            _animationController ??= _schematicObject.AnimationController;
            if (_animationController.Animators.Count == 0)
                return;

            _animationController.Play(stateName);
        }
        catch (Exception e)
        {
            Log.Debug($"[Tentacle] Failed to play schematic animation '{stateName}': {e.Message}");
        }
    }

    private static void DisableSchematicCollision(SchematicObject schematic)
    {
        foreach (PrimitiveObjectToy primitive in schematic.GetComponentsInChildren<PrimitiveObjectToy>(true))
            primitive.NetworkPrimitiveFlags &= ~PrimitiveFlags.Collidable;

        foreach (Collider collider in schematic.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
    }

    protected override void OnDestroy()
    {
        if (_isDestroying)
            return;

        _isDestroying = true;

        if (_updateHandle.IsRunning)
            Timing.KillCoroutines(_updateHandle);

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

        SchematicObject? schematic = _schematicObject;
        _schematicObject = null;
        _animationController = null;

        if (schematic != null)
        {
            PlayDestroyingAnimation(schematic);
            Timing.CallDelayed(DestroyAnimationDuration, () => DestroySchematic(schematic));
        }

        base.OnDestroy();
    }

    private static void PlayDestroyingAnimation(SchematicObject schematic)
    {
        try
        {
            ProjectMER.Features.AnimationController animator = schematic.AnimationController;
            if (animator.Animators.Count > 0)
                animator.Play("destroying");
        }
        catch (Exception e)
        {
            Log.Debug($"[Tentacle] Failed to play schematic animation 'destroying': {e.Message}");
        }
    }

    private static void DestroySchematic(SchematicObject schematic)
    {
        try
        {
            ProjectMER.Features.AnimationController animator = schematic.AnimationController;
            if (animator.Animators.Count > 0)
                animator.Stop();
        }
        catch
        {
            // ignore
        }

        try
        {
            schematic.Destroy();
        }
        catch
        {
            // ignore
        }
    }

    private static void OnSpawningRagdoll(Exiled.Events.EventArgs.Player.SpawningRagdollEventArgs ev)
    {
        if (ev?.Player != null && TentacleNpcIds.Contains(ev.Player.Id))
            ev.IsAllowed = false;
    }
}
